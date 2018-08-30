using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sir.Core;

namespace Sir.Store
{
    /// <summary>
    /// Write session targetting a single collection.
    /// </summary>
    public class WriteSession : CollectionSession
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly PagedPostingsReader _postingsReader;
        private readonly Dictionary<long, VectorNode> _dirty;
        private readonly ProducerConsumerQueue<IndexJob> _indexQueue;
        private readonly ProducerConsumerQueue<BuildJob> _buildQueue;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;

        public WriteSession(
            ulong collectionId, 
            LocalStorageSessionFactory sessionFactory, 
            ITokenizer tokenizer) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateLogWriter("writesession");
            _indexQueue = new ProducerConsumerQueue<IndexJob>(Write);
            _buildQueue = new ProducerConsumerQueue<BuildJob>(Write);

            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));
            PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collectionId)));
            VectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId)));
            Index = sessionFactory.GetCollectionIndex(collectionId);

            _vals = new ValueWriter(ValueStream);
            _keys = new ValueWriter(KeyStream);
            _docs = new DocWriter(DocStream);
            _valIx = new ValueIndexWriter(ValueIndexStream);
            _keyIx = new ValueIndexWriter(KeyIndexStream);
            _docIx = new DocIndexWriter(DocIndexStream);
            _postingsReader = new PagedPostingsReader(PostingsStream);
            _dirty = new Dictionary<long, VectorNode>();
        }

        public void Remove(IEnumerable<IDictionary> data)
        {
            var postingsWriter = new PagedPostingsWriter(PostingsStream);

            foreach (var model in data)
            {
                var docId = (ulong)model["__docid"];

                foreach (var key in model.Keys)
                {
                    var keyStr = key.ToString();
                    var keyHash = keyStr.ToHash();
                    var fieldIndex = GetIndex(keyHash);

                    if (fieldIndex == null)
                    {
                        continue;
                    }

                    var val = (IComparable)model[key];
                    var str = val as string;
                    var tokens = new HashSet<string>();

                    if (str == null || keyStr[0] == '_')
                    {
                        tokens.Add(val.ToString());

                    }
                    else
                    {
                        var tokenlist = _tokenizer.Tokenize(str).ToList();
                        foreach (var token in tokenlist)
                        {
                            tokens.Add(token);
                        }
                    }

                    foreach (var token in tokens)
                    {
                        // 1. find node
                        // 2. get postings list
                        // 3. find docId offset
                        // 2. flag document as deleted

                        var match = fieldIndex.ClosestMatch(token);

                        if (match.Highscore < VectorNode.IdenticalAngle)
                        {
                            continue;
                        }

                        var postings = _postingsReader.Read(match.PostingsOffset);

                        foreach (var posting in postings)
                        {
                            if (posting == docId)
                            {
                                postingsWriter.FlagAsDeleted(match.PostingsOffset, docId);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void Write(IEnumerable<IDictionary> models, bool writeToIndex = false)
        {
            foreach (var model in models)
            {
                var docId = _docIx.GetNextDocId();
                var docMap = new List<(long keyId, long valId)>();

                foreach (var key in model.Keys)
                {
                    var keyStr = key.ToString();
                    var keyHash = keyStr.ToHash();
                    var val = (IComparable)model[key];
                    var str = val as string;
                    long keyId, valId;

                    if (!SessionFactory.TryGetKeyId(keyHash, out keyId))
                    {
                        // We have a new key!

                        // store key
                        var keyInfo = _keys.Append(keyStr);
                        keyId = _keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                        SessionFactory.PersistKeyMapping(keyHash, keyId);
                    }

                    // store value
                    var valInfo = _vals.Append(val);
                    valId = _valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);

                    // store refs to keys and values
                    docMap.Add((keyId, valId));
                }

                var docMeta = _docs.Append(docMap);
                _docIx.Append(docMeta.offset, docMeta.length);

                model.Add("__docid", docId);
            }

            if (writeToIndex)
            {
                WriteToIndex(new IndexJob(CollectionId, models));
            }
        }

        public void WriteToIndex(IndexJob job)
        {
            _indexQueue.Enqueue(job);
        }

        private class BuildJob
        {
            public ulong CollectionId { get; }
            public ulong DocId { get; }
            public IEnumerable<string> Tokens { get; }
            public VectorNode Index { get; }

            public BuildJob(ulong collectionId, ulong docId, IEnumerable<string> tokens, VectorNode index)
            {
                CollectionId = collectionId;
                DocId = docId;
                Tokens = tokens;
                Index = index;
            }
        }

        private void Write(BuildJob job)
        {
            try
            {
                foreach (var token in job.Tokens)
                {
                    job.Index.Add(new VectorNode(token, job.DocId));
                }
            }
            catch (Exception ex)
            {
                _log.Log(ex.ToString());

                throw;
            }
        }

        private void Write(IndexJob job)
        {
            try
            {
                var docCount = 0;
                var timer = new Stopwatch();
                timer.Start();

                foreach (var doc in job.Documents)
                {
                    var docId = (ulong)doc["__docid"];

                    var keys = doc.Keys
                        .Cast<string>()
                        .Where(x => !x.StartsWith("__"));

                    foreach(var key in keys)
                    {
                        var keyHash = key.ToHash();
                        var keyId = SessionFactory.GetKeyId(keyHash);
                        VectorNode ix;

                        if (!_dirty.TryGetValue(keyId, out ix))
                        {
                            ix = GetIndex(keyHash) ?? new VectorNode();
                            _dirty.Add(keyId, ix);
                        }

                        var val = (IComparable)doc[key];
                        var str = val as string;
                        var tokens = new HashSet<string>();

                        if (str == null || key[0] == '_')
                        {
                            tokens.Add(val.ToString());
                        }
                        else
                        {
                            var tokenlist = _tokenizer.Tokenize(str);

                            foreach (var token in tokenlist)
                            {
                                tokens.Add(token);
                            }
                        }

                        _buildQueue.Enqueue(new BuildJob(CollectionId, docId, tokens, ix));
                    }

                    if (++docCount == 100)
                    {
                        _log.Log(string.Format("analyzed doc {0}", doc["__docid"]));
                        docCount = 0;
                    }
                }

                _log.Log(string.Format("executed {0} analyze job in {1}", 
                    job.CollectionId, timer.Elapsed));
            }
            catch (Exception ex)
            {
                _log.Log(ex.ToString());

                throw;
            }
        }

        public bool CommitToIndex()
        {
            try
            {
                if (_dirty.Count > 0)
                {
                    _log.Log(string.Format("committing {0} indexes to {1}", _dirty.Count, CollectionId));
                }

                foreach (var node in _dirty)
                {
                    var keyId = node.Key;
                    //var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));

                    //using (var ixStream = new FileStream(ixFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    //{
                    //    node.Value.Serialize(ixStream, VectorStream, PostingsStream);
                    //}

                    //node.Value.Serialize(PostingsStream);

                    //var size = node.Value.Size();

                    //_log.Log(string.Format("serialized index. col: {0} key_id:{1} w:{2} d:{3}",
                    //    CollectionId, keyId, size.width, size.depth));

                    SessionFactory.AddIndex(CollectionId, keyId, node.Value);

                    _log.Log(string.Format("refreshed index col: {0} key_id:{1}",
                        CollectionId, keyId));
                }

                if (_dirty.Count > 0)
                {
                    _log.Log(string.Format("committed {0} indexes to {1}", _dirty.Count, CollectionId));
                }

                return true;
            }
            catch (Exception ex)
            {
                _log.Log(ex.ToString());

                throw;
            }
        }

        private bool _disposed;
        private bool _committed;

        public override void Dispose()
        {
            if (!_disposed)
            {
                _indexQueue.Dispose();
                _disposed = true;
            }

            if (!_committed)
            {
                CommitToIndex();
                _committed = true;
            }

            base.Dispose();
        }
    }
}