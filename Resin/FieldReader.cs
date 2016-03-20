using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class FieldReader
    {
        // tokens/docids/positions
        private readonly IDictionary<string, IDictionary<int, IList<int>>> _tokens;

        public FieldReader(IDictionary<string, IDictionary<int, IList<int>>> tokens)
        {
            _tokens = tokens;
        }

        public static FieldReader Load(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                var terms = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(file);
                return new FieldReader(terms);
            }
        }

        public IEnumerable<TokenInfo> GetAllTokens()
        {
            return _tokens.Select(t=>new TokenInfo
            {
                Token = t.Key,
                Count = t.Value.Values.Select(l=>l.Count).Sum()
            });
        } 

        public IDictionary<int, IList<int>> GetPostings(string token)
        {
            IDictionary<int, IList<int>> postings;
            if (!_tokens.TryGetValue(token, out postings))
            {
                return null;
            }
            return postings;
        }
    }
}