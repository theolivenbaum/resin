using Microsoft.Extensions.Logging;
using Sir.Search;
using Sir.VectorSpace;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sir.Mnist
{
    /// <summary>
    /// Creates a vector index of the MNIST database.
    /// </summary>
    /// <example>
    /// indexmnist --imageFileName C:\temp\mnist\train-images.idx3-ubyte --labelFileName C:\temp\mnist\train-labels.idx1-ubyte --collection mnist
    /// </example>
    /// <example>
    /// indexmnist --imageFileName C:\temp\mnist\t10k-images.idx3-ubyte --labelFileName C:\temp\mnist\t10k-labels.idx1-ubyte --collection mnist
    /// </example>
    public class IndexMnistCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var time = Stopwatch.StartNew();
            var collectionId = args["collection"].ToHash();
            var images = new MnistReader(args["imageFileName"], args["labelFileName"]).Read();
            var count = 0;
            VectorNode tree;

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), logger))
            {
                sessionFactory.Truncate(collectionId);

                using (var writeSession = sessionFactory.CreateWriteSession(collectionId))
                {
                    var debugger = new IndexDebugger();
                    var keyId = writeSession.EnsureKeyExists("image");

                    using (var indexSession = sessionFactory.CreateIndexSession(collectionId, new ImageModel()))
                    {
                        foreach (var image in images)
                        {
                            var document = new Dictionary<string, object>() { { "label", image.Label } };
                            var storeFields = new HashSet<string> { "label" };
                            var documentId = writeSession.Put(document, storeFields);

                            indexSession.Put(documentId, keyId, image);

                            count++;

                            var debugInfo = debugger.GetDebugInfo(indexSession);

                            if (debugInfo != null)
                            {
                                logger.LogInformation(debugInfo);
                            }
                        }

                        tree = indexSession.GetInMemoryIndex(keyId);
                    }
                }
            }

            Print(tree);

            logger.LogInformation($"indexed {count} mnist images in {time.Elapsed}");
        }

        private static void Print(VectorNode tree)
        {
            var diagram = PathFinder.Visualize(tree);
            File.WriteAllText(@"c:\temp\mnisttree.txt", diagram);
        }
    }
}
