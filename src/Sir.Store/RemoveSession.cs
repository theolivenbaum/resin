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
    public class RemoveSession : CollectionSession
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly PagedPostingsReader _postingsReader;
        private readonly Dictionary<long, VectorNode> _dirty;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;

        public RemoveSession(
            ulong collectionId, 
            LocalStorageSessionFactory sessionFactory, 
            ITokenizer tokenizer) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("removesession");

            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));
            //PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collectionId)));
            //VectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId)));
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
    }
}