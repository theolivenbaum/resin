using Microsoft.Extensions.Logging;
using Sir.Documents;
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
            VectorNode tree;
            var debugger = new IndexDebugger(logger);
            var model = new LinearClassifierImageModel();
            using (var sessionFactory = new StreamFactory(logger))
            {
                sessionFactory.Truncate(dataDirectory, collectionId);

                using (var writeSession = new WriteSession(new DocumentWriter(dataDirectory, collectionId, sessionFactory)))
                using (var indexSession = new IndexSession<IImage>(model, model))
                {
                    var imageIndexId = writeSession.EnsureKeyExists("image");

                    foreach (var image in images)
                    {
                        var imageField = new Field("image", image.Pixels, index: true, store: true);
                        var labelField = new Field("label", image.Label, index: false, store: true);
                        var document = new Document(new Field[] { imageField, labelField });

                        writeSession.Put(document);
                        indexSession.Put(document.Id, imageField.KeyId, image);

                        debugger.Step(indexSession);
                    }

                    var indices = indexSession.GetInMemoryIndex();

                    tree = indices[imageIndexId];

                    using (var stream = new WritableIndexStream(dataDirectory, collectionId, sessionFactory, logger: logger))
                    {
                        stream.Write(indices);
                    }
                }
            }

            logger.LogInformation($"indexed {debugger.Steps} mnist images in {time.Elapsed}");

            Print(tree);
        }

        private static void Print(VectorNode tree)
        {
            File.WriteAllText(@"c:\temp\mnisttree.txt", PathFinder.Visualize(tree));
        }
    }
}