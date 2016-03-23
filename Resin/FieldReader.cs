using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class FieldReader
    {
        // tokens/docids/term frequency
        private readonly IDictionary<string, IDictionary<int, int>> _tokens;

        private readonly Trie _trie;

        public FieldReader(IDictionary<string, IDictionary<int,int>> tokens, Trie trie)
        {
            _tokens = tokens;
            _trie = trie;
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

        public IEnumerable<TokenInfo> GetAllTokens()
        {
            return _tokens.Select(t=>new TokenInfo
            {
                Token = t.Key,
                Count = t.Value.Values.Sum()
            });
        }

        public IEnumerable<string> GetTokens(string prefix)
        {
            return _trie.GetTokens(prefix);
        } 

        public IDictionary<int, int> GetPostings(string token)
        {
            IDictionary<int, int> postings;
            if (!_tokens.TryGetValue(token, out postings))
            {
                return null;
            }
            return postings;
        }
    }
}