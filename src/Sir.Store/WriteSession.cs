using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class WriteSession : Session
    {
        private readonly IDictionary<string, VectorNode> _dirty;

        public WriteSession(string directory, ulong collectionId, SessionFactory sessionFactory) 
            : base(directory, collectionId, sessionFactory)
        {
            _dirty = new Dictionary<string, VectorNode>();
        }

        public void Write(IEnumerable<IDictionary> data, ITokenizer tokenizer)
        {
            var vals = new ValueWriter(ValueStream);
            var keys = new ValueWriter(KeyStream);
            var docs = new DocWriter(DocStream);
            var valIx = new ValueIndexWriter(ValueIndexStream);
            var keyIx = new ValueIndexWriter(KeyIndexStream);
            var docIx = new DocIndexWriter(DocIndexStream);

            foreach (var model in data)
            {
                var docId = docIx.GetNextDocId();
                var docMap = new List<(uint keyId, uint valId)>();

                foreach (var key in model.Keys)
                {
                    var keyStr = key.ToString();
                    var keyHash = keyStr.ToHash();
                    var fieldIndex = GetIndex(keyHash);
                    var val = (IComparable)model[key];
                    var str = val as string;
                    var indexTokens = new List<Term>();
                    uint keyId, valId;

                    if (str != null) //TODO: implement numeric index
                    {
                        foreach (var token in tokenizer.Tokenize(str))
                        {
                            indexTokens.Add(new Term(keyStr, token));
                        }
                    }

                    if (fieldIndex == null)
                    {
                        // We have a new key!

                        // store key
                        var keyInfo = keys.Append(keyStr);
                        keyId = keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                        SessionFactory.AddKey(keyHash, keyId);

                        // add new index to global in-memory tree
                        fieldIndex = new VectorNode();
                        Index.Add(keyId, fieldIndex);
                    }
                    else
                    {
                        keyId = SessionFactory.GetKey(keyHash);
                    }

                    foreach (var token in indexTokens)
                    {
                        var match = fieldIndex.ClosestMatch((string)token.Value);

                        if (match.Highscore < VectorNode.IdenticalAngle)
                        {
                            // We have a new unique value!

                            // store value
                            var valInfo = vals.Append(val);
                            valId = valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);
                        }
                        else
                        {
                            valId = match.ValueId;
                        }

                        // add posting to index
                        fieldIndex.Add((string)token.Value, valId, docId);

                        // store refs to keys and values
                        docMap.Add((keyId, valId));
                    }

                    var indexName = string.Format("{0}.{1}", CollectionId, keyId);
                    if (!_dirty.ContainsKey(indexName))
                    {
                        _dirty.Add(indexName, fieldIndex);
                    }
                }

                var docMeta = docs.Append(docMap);
                docIx.Append(docMeta.offset, docMeta.length);
            }
        }

        public override void Dispose()
        {
            foreach (var node in _dirty)
            {
                var fn = Path.Combine(Dir, node.Key + ".ix");
                var fileMode = File.Exists(fn) ? FileMode.Truncate : FileMode.Append;

                using (var stream = new FileStream(fn, fileMode, FileAccess.Write, FileShare.None))
                {
                    node.Value.Serialize(stream, VectorStream, PostingsStream);
                }
            }

            ValueStream.Flush();
            KeyStream.Flush();
            DocStream.Flush();
            ValueIndexStream.Flush();
            KeyIndexStream.Flush();
            DocIndexStream.Flush();
            PostingsStream.Flush();
            VectorStream.Flush();

            base.Dispose();
        }
    }
}
