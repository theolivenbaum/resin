using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Write session targetting a single collection.
    /// </summary>
    public class WriteSession : CollectionSession
    {
        private readonly IDictionary<long, VectorNode> _dirty;
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly PagedPostingsReader _postingsReader;

        public WriteSession(ulong collectionId, LocalStorageSessionFactory sessionFactory) 
            : base(collectionId, sessionFactory)
        {
            _dirty = new Dictionary<long, VectorNode>();

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
        }

        public void Remove(IEnumerable<IDictionary> data, ITokenizer tokenizer)
        {
            var postingsWriter = new PagedPostingsWriter(PostingsStream);

            foreach (var model in data)
            {
                var docId = (ulong)model["_docid"];

                foreach (var key in model.Keys)
                {
                    var keyStr = key.ToString();
                    var keyHash = keyStr.ToHash();
                    var fieldIndex = GetInMemoryIndex(keyHash);

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
                        var tokenlist = tokenizer.Tokenize(str).ToList();
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

        public void Write(IEnumerable<IDictionary> data, ITokenizer tokenizer)
        {
            foreach (var model in data)
            {
                var docId = _docIx.GetNextDocId();
                var docMap = new List<(long keyId, long valId)>();

                foreach (var key in model.Keys)
                {
                    var keyStr = key.ToString();
                    var keyHash = keyStr.ToHash();
                    var fieldIndex = GetDirtyOrFreshIndexFromMemory(keyHash);
                    var val = (IComparable)model[key];
                    var str = val as string;
                    var tokens = new HashSet<string>();
                    long keyId, valId;

                    if (str == null || keyStr[0] == '_') 
                    {
                        tokens.Add(val.ToString());
                    }
                    else
                    {
                        var tokenlist = tokenizer.Tokenize(str).ToList();

                        foreach (var token in tokenlist)
                        {
                            tokens.Add(token);
                        }
                    }

                    if (fieldIndex == null)
                    {
                        // We have a new key!

                        // store key
                        var keyInfo = _keys.Append(keyStr);
                        keyId = _keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                        SessionFactory.AddKey(keyHash, keyId);

                        // create new index
                        fieldIndex = new VectorNode();

                        SessionFactory.Add(CollectionId, keyId, fieldIndex);
                    }
                    else
                    {
                        keyId = SessionFactory.GetKey(keyHash);
                    }

                    if (!_dirty.ContainsKey(keyId)) _dirty.Add(keyId, fieldIndex);

                    // store value
                    var valInfo = _vals.Append(val);
                    valId = _valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);

                    // store refs to keys and values
                    docMap.Add((keyId, valId));

                    foreach (var token in tokens)
                    {
                        // add nodes to in-memory index
                        fieldIndex.Add(new VectorNode(token, docId));
                    }
                }

                var docMeta = _docs.Append(docMap);
                _docIx.Append(docMeta.offset, docMeta.length);
            }

            // add nodes to disk-based index
            foreach (var node in _dirty)
            {
                var keyId = node.Key;
                var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));

                using (var ixStream = new FileStream(ixFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    node.Value.Serialize(ixStream, VectorStream, PostingsStream);
                }
            }
        }

        private VectorNode GetDirtyOrFreshIndexFromMemory(ulong keyHash)
        {
            long keyId;

            if (!SessionFactory.TryGetKeyId(keyHash, out keyId))
            {
                return null;
            }

            VectorNode dirty;
            if (_dirty.TryGetValue(keyId, out dirty))
            {
                return dirty;
            }

            return GetInMemoryIndex(keyHash);
        }
    }
}