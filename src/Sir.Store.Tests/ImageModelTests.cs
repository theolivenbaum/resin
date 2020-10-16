using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Sir.Mnist;
using Sir.VectorSpace;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sir.Search.Tests
{
    public class ImageModelTests
    {
        private ILoggerFactory _loggerFactory;
        private SessionFactory _sessionFactory;
        private IImage[] _data;

        [Test]
        public void Can_merge_or_add_supervised_in_memory()
        {
            var model = new ImageModel();
            var tree = GraphBuilder.Train(model, _data);

            Print(tree);

            Assert.DoesNotThrow(() =>
            {
                var count = 0;
                var errors = 0;

                foreach (var word in _data)
                {
                    foreach (var queryVector in model.Tokenize(word))
                    {
                        var hit = PathFinder.ClosestMatch(tree, queryVector, model);

                        if (hit == null)
                        {
                            throw new Exception($"unable to find {word} in tree.");
                        }

                        if (!hit.Node.Vector.Label.Equals(word.Label))
                        {
                            errors++;
                        }

                        Debug.WriteLine($"{word} matched with {hit.Node.Vector.Label} with {hit.Score * 100}% certainty.");

                        count++;
                    }
                }

                var errorRate = (float)errors / count;

                if (errorRate > 0)
                {
                    throw new Exception($"error rate: {errorRate * 100}%. too many errors.");
                }
            });
        }

        [Test]
        public void Can_traverse_index_in_memory()
        {
            var model = new ImageModel();
            var tree = GraphBuilder.CreateTree(model, _data);

            Print(tree);

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
            var model = new ImageModel();
            var tree = GraphBuilder.CreateTree(model, _data);

            using (var indexStream = new MemoryStream())
            using (var vectorStream = new MemoryStream())
            using (var pageStream = new MemoryStream())
            {
                using (var writer = new ColumnStreamWriter(indexStream, keepStreamOpen:true))
                {
                    writer.CreatePage(tree, vectorStream, new PageIndexWriter(pageStream, keepStreamOpen:true));
                }

                pageStream.Position = 0;

                Assert.DoesNotThrow(() =>
                {
                    using (var reader = new ColumnStreamReader(new PageIndexReader(pageStream), indexStream, vectorStream, _sessionFactory, _loggerFactory.CreateLogger<ColumnStreamReader>()))
                    {
                        foreach (var image in _data)
                        {
                            foreach (var queryVector in model.Tokenize(image))
                            {
                                var hit = reader.ClosestMatch(queryVector, model);

                                if (hit == null)
                                {
                                    throw new Exception($"unable to find {image} in tree.");
                                }

                                if (hit.Score < model.IdenticalAngle)
                                {
                                    throw new Exception($"unable to score {image}.");
                                }

                                Debug.WriteLine($"{image} matched vector in disk with {hit.Score * 100}% certainty.");
                            }
                        }
                    }
                });
            }
        }

        [SetUp]
        public void Setup()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Sir.DbUtil.Program", LogLevel.Debug)
                    .AddDebug();
            });

            _sessionFactory = new SessionFactory(
                new KeyValueConfiguration("sir.ini"),
                _loggerFactory.CreateLogger<SessionFactory>());

            _data = new MnistReader(
                @"C:\temp\mnist\t10k-images.idx3-ubyte",
                @"C:\temp\mnist\t10k-labels.idx1-ubyte").Read().Take(100).ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            _sessionFactory.Dispose();
        }

        private static void Print(VectorNode tree)
        {
            var diagram = PathFinder.Visualize(tree);
            File.WriteAllText(@"c:\temp\imagemodeltesttree.txt", diagram);
            Debug.WriteLine(diagram);
        }
    }
}