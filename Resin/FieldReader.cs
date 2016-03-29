using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class FieldReader
    {
        // terms/docids/term frequency
        private readonly IDictionary<string, IDictionary<int, int>> _terms;
        
        // prefix tree
        private readonly Trie _trie;
        
        private readonly int _docCount;

        public int DocCount { get { return _docCount; } }
        public Trie Trie { get { return _trie; } }
        public IDictionary<string, IDictionary<int, int>> Terms { get { return _terms; } } 

        public FieldReader(IDictionary<string, IDictionary<int,int>> terms, Trie trie)
        {
            if (terms == null) throw new ArgumentNullException("terms");
            if (trie == null) throw new ArgumentNullException("trie");

            _terms = terms;
            _trie = trie;
            _docCount = _terms.Values.SelectMany(x => x.Keys).ToList().Distinct().Count();
        }

        public IList<int> Docs()
        {
            var docs = new List<int>();
            foreach (var term in _terms)
            {
                docs.AddRange(term.Value.Keys);
            }
            return docs;
        }

        public static FieldReader Load(string fileName)
        {
            var trieFileName = fileName + ".tri";
            var trie = Trie.Load(trieFileName); 
            using (var file = File.OpenRead(fileName))
            {
                var terms = Serializer.Deserialize<Dictionary<string, IDictionary<int, int>>>(file);
                return new FieldReader(terms, trie);
            }
        }

        public void Merge(FieldReader latter)
        {
            foreach (var newTerm in latter._terms)
            {
                IDictionary<int, int> t;
                if (!_terms.TryGetValue(newTerm.Key, out t))
                {
                    _terms.Add(newTerm);
                    _trie.Add(newTerm.Key);
                }
                else
                {
                    foreach (var posting in newTerm.Value)
                    {
                        t[posting.Key] = posting.Value;
                    }
                }
            }
        }

        public IEnumerable<TokenInfo> GetAllTokens()
        {
            return _terms.Select(t=>new TokenInfo
            {
                Token = t.Key,
                Count = t.Value.Values.Sum()
            });
        }

        public IEnumerable<string> GetTokens(string prefix)
        {
            return _trie.Prefixed(prefix);
        }

        public IEnumerable<string> GetSimilar(string word, int edits)
        {
            return _trie.Similar(word, edits);
        } 

        public IDictionary<int, int> GetPostings(string token)
        {
            IDictionary<int, int> postings;
            if (!_terms.TryGetValue(token, out postings))
            {
                return null;
            }
            return postings;
        }
    }
}