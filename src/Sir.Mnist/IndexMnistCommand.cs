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
    /// indexmnist --dataDirectory c:\data\resin --imageFileName C:\temp\mnist\train-images.idx3-ubyte --labelFileName C:\temp\mnist\train-labels.idx1-ubyte --collection mnist
    /// </example>
    /// <example>
    /// indexmnist --dataDirectory c:\data\resin --imageFileName C:\temp\mnist\t10k-images.idx3-ubyte --labelFileName C:\temp\mnist\t10k-labels.idx1-ubyte --collection mnist
    /// </example>
    public class IndexMnistCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var time = Stopwatch.StartNew();
            var dataDirectory = args["dataDirectory"];
            var collectionId = args["collection"].ToHash();
            var images = new MnistReader(args["imageFileName"], args["labelFileName"]).Read();
            var count = 0;
            VectorNode tree;

            using (var sessionFactory = new SessionFactory(dataDirectory, logger))
            {
                sessionFactory.Truncate(collectionId);

                using (var writeSession = sessionFactory.CreateWriteSession(collectionId))
                {
                    var debugger = new IndexDebugger(logger);
                    var keyId = writeSession.EnsureKeyExists("image");

                    using (var indexSession = sessionFactory.CreateIndexSession(new LinearClassifierImageModel()))
                    {
                        foreach (var image in images)
                        {
                            var document = new Search.Document(new Field[] { new Field("image", image.Label, index: false, store: true) });
                            writeSession.Put(document);

                            indexSession.Put(document.Id, keyId, image);

                            count++;

                            debugger.Step(indexSession);
                        }

                        tree = indexSession.InMemoryIndex[keyId];

                        using (var stream = new WritableIndexStream(collectionId, sessionFactory, logger:logger))
                        {
                            stream.Write(indexSession.InMemoryIndex);
                        }
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
