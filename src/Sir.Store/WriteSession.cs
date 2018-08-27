using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));
            PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collectionId)));
            VectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId)));
            Index = sessionFactory.GetIndex(collectionId);

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
                var docId = (ulong)model["_docid"];

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

        public void Write(IEnumerable<IDictionary> models)
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
                    VectorNode ix;

                    if (!SessionFactory.TryGetKeyId(keyHash, out keyId))
                    {
                        // We have a new key!

                        // store key
                        var keyInfo = _keys.Append(keyStr);
                        keyId = _keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                        SessionFactory.AddKey(keyHash, keyId);

                        // create new index
                        ix = new VectorNode();
                        SessionFactory.AddIndex(CollectionId, keyId, ix);
                    }
                    else
                    {
                        ix = GetIndex(keyHash);
                    }

                    if (!_dirty.ContainsKey(keyId))
                    {
                        _dirty.Add(keyId, ix);
                    }

                    // store value
                    var valInfo = _vals.Append(val);
                    valId = _valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);

                    // store refs to keys and values
                    docMap.Add((keyId, valId));
                }

                var docMeta = _docs.Append(docMap);
                _docIx.Append(docMeta.offset, docMeta.length);

                model.Add("__docId", docId);
            }

            _indexQueue.Enqueue(new IndexJob(CollectionId, models));
        }

        private void Write(IndexJob job)
        {
            foreach (var doc in job.Documents)
            {
                foreach (var key in doc.Keys)
                {
                    var keyStr = key.ToString();

                    if (keyStr.StartsWith("__")) continue;

                    var keyHash = keyStr.ToHash();
                    var keyId = SessionFactory.GetKeyId(keyHash);
                    var ix = _dirty[keyId];
                    var val = (IComparable)doc[key];
                    var str = val as string;
                    var tokens = new HashSet<string>();

                    if (str == null || keyStr[0] == '_')
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

                    var docId = (ulong)doc["__docId"];

                    foreach (var token in tokens)
                    {
                        ix.Add(new VectorNode(token, docId));
                    }
                }
            }
        }

        public bool CommitToIndex()
        {
            try
            {
                foreach (var node in _dirty)
                {
                    var keyId = node.Key;
                    //var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));

                    node.Value.Serialize(VectorStream, PostingsStream);

                    var size = node.Value.Size();

                    _log.Log(string.Format("serialized index. col: {0} key_id:{1} w:{2} d:{3}", 
                        CollectionId, keyId, size.width, size.depth));
                }

                return true;
            }
            catch (Exception ex)
            {
                _log.Log(ex.ToString());

                return false;
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