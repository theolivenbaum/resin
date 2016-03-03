using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class FieldReader
    {
        private readonly IDictionary<string, IDictionary<int, IList<int>>> _field;

        public FieldReader(IDictionary<string, IDictionary<int, IList<int>>> field)
        {
            _field = field;
        }

        public static FieldReader LoadAndMerge(params string[] files)
        {
            IDictionary<string, IDictionary<int, IList<int>>> aggregated = null;
            foreach (var file in files)
            {
                using (var fs = File.OpenRead(file))
                {
                    var field = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(fs);
                    if (aggregated == null)
                    {
                        aggregated = field;
                    }
                    else
                    {
                        foreach (var term in field)
                        {
                            IDictionary<int, IList<int>> docs;
                            if (aggregated.TryGetValue(term.Key, out docs))
                            {
                                foreach (var positions in term.Value)
                                {
                                    IList<int> pos;
                                    if (docs.TryGetValue(positions.Key, out pos))
                                    {
                                        docs[positions.Key] = pos.Concat(positions.Value).ToList();
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

        public static FieldReader Load(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                var field = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(file);
                return new FieldReader(field);
            }
        }

        public IDictionary<int, IList<int>> GetDocPosition(string termValue)
        {
            IDictionary<int, IList<int>> docPositions;
            if (!_field.TryGetValue(termValue, out docPositions))
            {
                return null;
            }
            return docPositions;
        }
    }
}
