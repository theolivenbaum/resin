using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static FieldReader LoadAndMerge(params string[] files)
        {
            IDictionary<string, IDictionary<int, IList<int>>> aggregated = null;
            foreach (var file in files)
            {
                using (var fs = File.OpenRead(file))
                {
                    var terms = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(fs);
                    if (aggregated == null)
                    {
                        aggregated = terms;
                    }
                    else
                    {
                        foreach (var term in terms)
                        {
                            IDictionary<int, IList<int>> docs;
                            if (aggregated.TryGetValue(term.Key, out docs))
                            {
                                foreach (var positions in term.Value)
                                {
                                    IList<int> docPos;
                                    if (docs.TryGetValue(positions.Key, out docPos))
                                    {
                                        docs[positions.Key] = docPos.Concat(positions.Value).ToList();
                                    }
                                    else
                                    {
                                        docs[positions.Key] = positions.Value;
                                    }
                                }
                            }
                            else
                            {
                                aggregated.Add(term.Key, term.Value);
                            }
                        }
                    }
                }
            }
            return new FieldReader(aggregated);
        }

        public ICollection<string> GetAllTerms()
        {
            return _terms.Keys;
        } 

        public static FieldReader Load(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                var terms = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(file);
                return new FieldReader(terms);
            }
        }

        public IDictionary<int, IList<int>> GetDocPosition(string termValue)
        {
            IDictionary<int, IList<int>> docPositions;
            if (!_terms.TryGetValue(termValue, out docPositions))
            {
                return null;
            }
            return docPositions;
        }
    }
}