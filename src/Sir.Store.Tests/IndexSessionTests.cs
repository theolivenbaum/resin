using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Sir.Document;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.Diagnostics;
using System.Linq;

namespace Sir.Tests
{
    public class IndexSessionTests
    {
        private SessionFactory _sessionFactory;

        private readonly string[] _data = new string[] { "apple", "apples", "apricote", "apricots", "avocado", "avocados", "banana", "bananas", "blueberry", "blueberries", "cantalope" };

        [Test]
        public void Can_produce_traversable_in_memory_index()
        {
            var model = new BagOfCharsModel();
            VectorNode tree;

            using (var indexSession = _sessionFactory.CreateIndexSession(model))
            {
                for (long i = 0; i < _data.Length; i++)
                {
                    indexSession.Put(i, 0, _data[i]);
                }

                tree = indexSession.GetInMemoryIndex(0);
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
        public void Can_search_streamed()
        {
            var model = new BagOfCharsModel();
            VectorNode index;
            const string collection = "Can_search_streamed";
            var collectionId = collection.ToHash();
            const string fieldName = "description";

            _sessionFactory.Truncate(collectionId);

            using (var writeSession = _sessionFactory.CreateWriteSession(collectionId))
            using (var indexSession = _sessionFactory.CreateIndexSession(model))
            {
                var keyId = writeSession.EnsureKeyExists(fieldName);

                for (long i = 0; i < _data.Length; i++)
                {
                    var data = _data[i];

                    writeSession.Put(new Search.Document(new Field[] { new Field(fieldName, data, index: true, store: true) }));

                    indexSession.Put(i, keyId, data);
                }

                index = indexSession.GetInMemoryIndex(keyId);

                using (var stream = new IndexFileStreamProvider(collectionId, _sessionFactory))
                {
                    stream.Write(indexSession.GetInMemoryIndex());
                }
            }

            Debug.WriteLine(PathFinder.Visualize(index));

            var queryParser = new QueryParser<string>(_sessionFactory, model);

            using (var searchSession = new SearchSession(_sessionFactory, model, new PostingsReader(_sessionFactory)))
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

                        var score = (double)document[SystemFields.Score];

                        if (score < model.IdenticalAngle)
                        {
                            throw new Exception($"unable to score {word}.");
                        }

                        Debug.WriteLine($"{word} matched with {score * 100}% certainty.");
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
                    .AddFilter("Sir.DbUtil.Program", LogLevel.Debug)
                    .AddDebug();
            });

            _sessionFactory = new SessionFactory(directory: @"c:\temp\sir_tests", logger: loggerFactory.CreateLogger<SessionFactory>());
        }

        [TearDown]
        public void TearDown()
        {
            _sessionFactory.Dispose();
        }
    }
}