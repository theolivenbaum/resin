using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.Store.Tests
{
    public class GoogleFeed : IEnumerable<IDictionary<string, object>>
    {
        private readonly IEnumerable<IDictionary<string, object>> _documents;

        public GoogleFeed(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                _documents = Deserialize(stream).Select(x => new Dictionary<string, object>
                {
                    {"title", (string)x["title"]},
                    {"body", (string)x["description"]},
                    {"_url", (string)x["link"]},
                });
            }
        }

        private static IEnumerable<IDictionary<string, object>> Deserialize(Stream stream)
        {
            var serializer = new JsonSerializer();

            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                return serializer.Deserialize<IEnumerable<IDictionary<string, object>>>(jsonTextReader);
            }
        }

        public IEnumerator<IDictionary<string, object>> GetEnumerator()
        {
            return _documents.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _documents.GetEnumerator();
        }
    }
}