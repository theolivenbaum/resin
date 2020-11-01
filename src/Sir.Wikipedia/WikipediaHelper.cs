using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Sir.Wikipedia
{
    public static class WikipediaHelper
    {
        public static IEnumerable<IDictionary> ReadWP(string fileName, int skip, int take)
        {
            if (Path.GetExtension(fileName).EndsWith("gz"))
            {
                using (var stream = File.OpenRead(fileName))
                using (var zip = new GZipStream(stream, CompressionMode.Decompress))
                using (var reader = new StreamReader(zip))
                {
                    return ReadJsonFile(reader, skip, take);
                }
            }

            using (var stream = File.OpenRead(fileName))
            using (var reader = new StreamReader(stream))
            {
                return ReadJsonFile(reader, skip, take);

            }
        }

        public static IEnumerable<IDictionary> ReadJsonFile(StreamReader reader, int skip, int take)
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

                var doc = JsonConvert.DeserializeObject<IDictionary>(line);

                if (doc.Contains("title"))
                {
                    yield return doc;
                    took++;
                }

                line = reader.ReadLine();
            }
        }
    }
}
