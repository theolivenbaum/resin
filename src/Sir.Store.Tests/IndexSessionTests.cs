using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.Diagnostics;
using System.IO;

namespace Sir.Tests
{
    public class IndexSessionTests
    {
        private SessionFactory _sessionFactory;

        private readonly string[] _data = new string[] { "apple", "apples", "apricote", "apricots", "avocado", "avocados", "banana", "bananas", "blueberry", "blueberries", "cantalope" };

        [Test]
        public void Can_build_in_memory_index()
        {
            var model = new BagOfCharsModel();
            var collectionId = "Can_index".ToHash();
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

            _sessionFactory = new SessionFactory(logger: loggerFactory.CreateLogger<SessionFactory>());
        }

        [TearDown]
        public void TearDown()
        {
            _sessionFactory.Dispose();
        }
    }
}