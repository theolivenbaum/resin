using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class FieldReader
    {
        // terms/docids/positions
        private readonly IDictionary<string, IDictionary<int, IList<int>>> _terms;

        public FieldReader(IDictionary<string, IDictionary<int, IList<int>>> terms)
        {
            _terms = terms;
        }

        public static FieldReader Load(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                var terms = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(file);
                return new FieldReader(terms);
            }
        }

        public ICollection<string> GetAllTerms()
        {
            return _terms.Keys;
        } 

        public IDictionary<int, IList<int>> GetDocPosition(string token)
        {
            IDictionary<int, IList<int>> docPositions;
            if (!_terms.TryGetValue(token, out docPositions))
            {
                return null;
            }
            return docPositions;
        }
    }
}