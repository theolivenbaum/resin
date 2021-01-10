using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Sir.Mnist;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sir.Tests
{
    public class ImageModelTests
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<ImageModelTests> _logger;
        private StreamFactory _sessionFactory;
        private IImage[] _data;
        private string _directory = @"c:\temp\sir_tests";

        [Test]
        public void Can_train_in_memory()
        {
            var model = new LinearClassifierImageModel();
            var tree = model.CreateTree(model, _data);

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

                Debug.WriteLine($"error rate: {errorRate}");
            });
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

            _logger = _loggerFactory.CreateLogger<ImageModelTests>();

            _sessionFactory = new StreamFactory(logger: _loggerFactory.CreateLogger<StreamFactory>());

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