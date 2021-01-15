using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sir.Tests
{
    public class TextModelTests
    {
        private ILoggerFactory _loggerFactory;
        private Database _sessionFactory;
        private string _directory = @"c:\temp\sir_tests";

        private readonly string[] _data = new string[] { "apple", "apples", "apricote", "apricots", "avocado", "avocados", "banana", "bananas", "blueberry", "blueberries", "cantalope" };

        [Test]
        public void Can_traverse_index_in_memory()
        {
            var model = new BagOfCharsModel();
            var tree = model.CreateTree(model, _data);

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
        public void Can_traverse_streamed()
        {
            var model = new BagOfCharsModel();
            var tree = model.CreateTree(model, _data);

            using (var indexStream = new MemoryStream())
            using (var vectorStream = new MemoryStream())
            using (var pageStream = new MemoryStream())
            {
                using (var writer = new ColumnWriter(indexStream, keepStreamOpen:true))
                {
                    writer.CreatePage(tree, vectorStream, new PageIndexWriter(pageStream, keepStreamOpen:true));
                }

                pageStream.Position = 0;

                Assert.DoesNotThrow(() =>
                {
                    using (var pageIndexReader = new PageIndexReader(pageStream))
                    using (var reader = new ColumnReader(pageIndexReader.ReadAll(), indexStream, vectorStream, _sessionFactory, _loggerFactory.CreateLogger<ColumnReader>()))
                    {
                        foreach (var word in _data)
                        {
                            foreach (var queryVector in model.Tokenize(word))
                            {
                                var hit = reader.ClosestMatch(queryVector, model);

                                if (hit == null)
                                {
                                    throw new Exception($"unable to find {word} in tree.");
                                }

                                if (hit.Score < model.IdenticalAngle)
                                {
                                    throw new Exception($"unable to score {word}.");
                                }

                                Debug.WriteLine($"{word} matched vector in disk with {hit.Score * 100}% certainty.");
                            }
                        }
                    }
                });
            }
        }

        [Test]
        public void Can_tokenize()
        {
            const string data = "Ferriman–Gallwey score"; // NOTE: string contains "En dash" character: https://unicode-table.com/en/#2013
            var model = new BagOfCharsModel();
            var tokens = model.Tokenize(data);
            var labels = tokens.Select(x => x.Label.ToString()).ToList();

            var t0 = data.Substring(0, 8);
            var t1 = data.Substring(9, 7);
            var t2 = data.Substring(17, 5);

            Assert.IsTrue(labels.Contains(t0));
            Assert.IsTrue(labels.Contains(t1));
            Assert.IsTrue(labels.Contains(t2));
        }

        [SetUp]
        public void Setup()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddDebug();
            });

            _sessionFactory = new Database(logger: _loggerFactory.CreateLogger<Database>());
        }

        [TearDown]
        public void TearDown()
        {
            _sessionFactory.Dispose();
        }
    }
}