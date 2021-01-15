using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Sir.Documents;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.Diagnostics;
using System.Linq;

namespace Sir.Tests
{
    public class IndexSessionTests
    {
        private Database _sessionFactory;
        private string _directory = @"c:\temp\sir_tests";

        private readonly string[] _data = new string[] { "apple", "apples", "apricote", "apricots", "avocado", "avocados", "banana", "bananas", "blueberry", "blueberries", "cantalope" };

        [Test]
        public void Can_produce_traversable_in_memory_index()
        {
            var model = new BagOfCharsModel();
            VectorNode tree;

            using (var indexSession = new IndexSession<string>(model, model))
            {
                for (long i = 0; i < _data.Length; i++)
                {
                    indexSession.Put(i, 0, _data[i]);
                }

                tree = indexSession.GetInMemoryIndex()[0];
            }

            Debug.WriteLine(PathFinder.Visualize(tree));

            Assert.DoesNotThrow(() => 
            {
                foreach (var word in _data)
                {
                    foreach (var queryVector in model.Tokenize(word))
                    {
                        var hit = PathFinder.ClosestMatch(tree, queryVector, model);

                        if (hit == null)
                        {
                            throw new Exception($"unable to find {word} in tree.");
                        }

                        if (hit.Score < model.IdenticalAngle)
                        {
                            throw new Exception($"unable to score {word}.");
                        }

                        Debug.WriteLine($"{word} matched with {hit.Node.Vector.Label} with {hit.Score * 100}% certainty.");
                    }
                }
            });
        }

        [Test]
        public void Can_search_filestreamed_with_multiple_pages()
        {
            var model = new BagOfCharsModel();
            const string collection = "Can_search_streamed_with_one_page_per_document";
            var collectionId = collection.ToHash();
            const string fieldName = "description";

            _sessionFactory.Truncate(_directory, collectionId);

            using (var stream = new WritableIndexStream(_directory, collectionId, _sessionFactory))
            using (var writeSession = new WriteSession(new DocumentWriter(_directory, collectionId, _sessionFactory)))
            {
                var keyId = writeSession.EnsureKeyExists(fieldName);

                for (long i = 0; i < _data.Length; i++)
                {
                    var data = _data[i];

                    using (var indexSession = new IndexSession<string>(model, model))
                    {
                        var doc = new Document(new Field[] { new Field(fieldName, data, index: true, store: true) });
                        
                        writeSession.Put(doc);
                        indexSession.Put(doc.Id, keyId, data);
                        stream.Write(indexSession.GetInMemoryIndex());
                    }
                }
            }

            var queryParser = new QueryParser<string>(_directory, _sessionFactory, model);

            using (var searchSession = new SearchSession(_directory, _sessionFactory, model))
            {
                Assert.DoesNotThrow(() =>
                {
                    foreach (var word in _data)
                    {
                        var query = queryParser.Parse(collection, word, fieldName, fieldName, and: true, or: false);
                        var result = searchSession.Search(query, 0, 1);
                        var document = result.Documents.FirstOrDefault();

                        if (document == null)
                        {
                            throw new Exception($"unable to find {word}.");
                        }

                        if (document.Score < model.IdenticalAngle)
                        {
                            throw new Exception($"unable to score {word}.");
                        }

                        Debug.WriteLine($"{word} matched with {document.Score * 100}% certainty.");
                    }
                });
            }
        }

        [Test]
        public void Can_search_streamed()
        {
            var model = new BagOfCharsModel();
            VectorNode index;
            const string collection = "Can_search_streamed";
            var collectionId = collection.ToHash();
            const string fieldName = "description";

            _sessionFactory.Truncate(_directory, collectionId);

            using (var writeSession = new WriteSession(new DocumentWriter(_directory, collectionId, _sessionFactory)))
            using (var indexSession = new IndexSession<string>(model, model))
            {
                var keyId = writeSession.EnsureKeyExists(fieldName);

                for (long i = 0; i < _data.Length; i++)
                {
                    var data = _data[i];
                    var doc = new Document(new Field[] { new Field(fieldName, data, index: true, store: true) });

                    writeSession.Put(doc);
                    indexSession.Put(doc.Id, keyId, data);
                }

                var indices = indexSession.GetInMemoryIndex();

                index = indices[keyId];

                using (var stream = new WritableIndexStream(_directory, collectionId, _sessionFactory))
                {
                    stream.Write(indices);
                }
            }

            Debug.WriteLine(PathFinder.Visualize(index));

            var queryParser = new QueryParser<string>(_directory, _sessionFactory, model);

            using (var searchSession = new SearchSession(_directory, _sessionFactory, model))
            {
                Assert.DoesNotThrow(() =>
                {
                    foreach (var word in _data)
                    {
                        var query = queryParser.Parse(collection, word, fieldName, fieldName, and: true, or: false);
                        var result = searchSession.Search(query, 0, 1);
                        var document = result.Documents.FirstOrDefault();

                        if (document == null)
                        {
                            throw new Exception($"unable to find {word}.");
                        }

                        if (document.Score < model.IdenticalAngle)
                        {
                            throw new Exception($"unable to score {word}.");
                        }

                        Debug.WriteLine($"{word} matched with {document.Score * 100}% certainty.");
                    }
                });
            }
        }

        [SetUp]
        public void Setup()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddDebug();
            });

            _sessionFactory = new Database(loggerFactory.CreateLogger<Database>());
        }

        [TearDown]
        public void TearDown()
        {
            _sessionFactory.Dispose();
        }
    }
}