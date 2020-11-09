using Newtonsoft.Json.Linq;
using Sir.Search;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Sir.Wikipedia
{
    public static class WikipediaHelper
    {
        public static IEnumerable<Search.Document> ReadWP(string fileName, int skip, int take, HashSet<string> fieldsToStore, HashSet<string> fieldsToIndex)
        {
            if (Path.GetExtension(fileName).EndsWith("gz"))
            {
                return ReadGZipJsonFile(fileName, skip, take, fieldsToStore, fieldsToIndex);
            }

            return ReadJsonFile(fileName, skip, take, fieldsToStore, fieldsToIndex);
        }

        public static IEnumerable<Search.Document> ReadGZipJsonFile(string fileName, int skip, int take, HashSet<string> fieldsToStore, HashSet<string> fieldsToIndex)
        {
            using (var stream = File.OpenRead(fileName))
            using (var zip = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(zip))
            {
                return ReadFile(reader, skip, take, fieldsToStore, fieldsToIndex);
            }
        }

        public static IEnumerable<Search.Document> ReadJsonFile(string fileName, int skip, int take, HashSet<string> fieldsToStore, HashSet<string> fieldsToIndex)
        {
            using (var stream = File.OpenRead(fileName))
            using (var reader = new StreamReader(stream))
            {
                return ReadFile(reader, skip, take, fieldsToStore, fieldsToIndex);
            }
        }

        private static IEnumerable<Search.Document> ReadFile(StreamReader reader, int skip, int take, HashSet<string> fieldsToStore, HashSet<string> fieldsToIndex)
        {
            var skipped = 0;
            var took = 0;

            //skip first line
            reader.ReadLine();

            var line = reader.ReadLine();

            while (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("]"))
            {
                if (took == take)
                    break;

                if (skipped++ < skip)
                {
                    continue;
                }

                var jobject = JObject.Parse(line);

                if (jobject.ContainsKey("title"))
                {
                    var fields = new List<Field>();

                    foreach (var kvp in jobject)
                    {
                        var store = fieldsToStore.Contains(kvp.Key);
                        var index = fieldsToIndex.Contains(kvp.Key);

                        if (store||index)
                            fields.Add(new Field(kvp.Key, kvp.Value.Value<object>(), index, store));
                    }

                    yield return new Search.Document(fields);
                    took++;
                }

                line = reader.ReadLine();
            }
        }
    }
}