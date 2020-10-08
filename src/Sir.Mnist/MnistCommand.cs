using Microsoft.Extensions.Logging;
using Sir.Search;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sir.Mnist
{
    /// <summary>
    /// Creates a vector index of the MNIST database.
    /// </summary>
    /// <example>
    /// mnist --imageFileName C:\temp\mnist\t10k-images.idx3-ubyte --labelFileName C:\temp\mnist\t10k-labels.idx1-ubyte --collection mnist
    /// </example>
    public class MnistCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var time = Stopwatch.StartNew();
            var collectionId = args["collection"].ToHash();
            var images = new MnistReader(args["imageFileName"], args["labelFileName"]).Read();
            var count = 0;

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), logger))
            using (var writeSession = sessionFactory.CreateWriteSession(collectionId))
            using (var indexSession = sessionFactory.CreateIndexSession(collectionId, new StreamModel()))
            {
                var debugger = new IndexDebugger();

                foreach (var image in images)
                {
                    var document = new Dictionary<string, object>() { { "label", image.Label } };
                    var storeFields = new HashSet<string> { "label" };
                    var documentId = writeSession.Put(document, storeFields);
                    var keyId = writeSession.EnsureKeyExists("label");

                    indexSession.Put(documentId, keyId, image.Pixels);

                    count++;

                    var debugInfo = debugger.GetDebugInfo(indexSession);

                    if (debugInfo != null)
                    {
                        logger.LogInformation(debugInfo);
                    }
                }
            }

            logger.LogInformation($"indexed {count} mnist images in {time.Elapsed}");
        }
    }
}
