using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Store.Tests
{
    public class ReadTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Can_write_and_scan()
        {
            var documents = new GoogleFeed("google-feed.json").Take(100).ToList();
            var model = new BocModel();
            var collection = "vectornodetests";
            var collectionId = collection.ToHash();
            var comparer = new DocumentComparer();

            using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
            {
                sessionFactory.Truncate(collectionId);

                sessionFactory.ExecuteWrite(new Job(collection, documents));

                Parallel.ForEach(documents, document =>
                //foreach (var document in documents)
                {
                    var terms = model.Tokenize((string)document["body"]);
                    var docId = (long)document["___docid"];

                    using (var reader = sessionFactory.CreateReadSession(collection, collectionId))
                    {
                        foreach (var embedding in terms.Embeddings)
                        {
                            var query = new Query(collectionId, new Term("body", new VectorNode(embedding)));
                            var result = reader.Read(query);

                            Assert.IsTrue(result.Docs.Contains(document, comparer));
                        }
                    }

                    Debug.WriteLine($"VALIDATED: {docId}");
                });
            }
        }
    }

    public class DocumentComparer : IEqualityComparer<IDictionary<string, object>>
    {
        public bool Equals(IDictionary<string, object> x, IDictionary<string, object> y)
        {
            return (long)x["___docid"] == (long)y["___docid"];
        }

        public int GetHashCode(IDictionary<string, object> obj)
        {
            return obj.GetHashCode();
        }
    }

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