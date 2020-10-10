﻿using Microsoft.Extensions.Logging;
using Sir.Search;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sir.Mnist
{
    /// <summary>
    /// Creates a vector index of the MNIST database.
    /// </summary>
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

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), logger))
            using (var writeSession = sessionFactory.CreateWriteSession(collectionId))
            using (var indexSession = sessionFactory.CreateIndexSession(collectionId, new ImageModel()))
            {
                var debugger = new IndexDebugger();
                var keyId = writeSession.EnsureKeyExists("image");

                foreach (var image in images)
                {
                    var document = new Dictionary<string, object>() { { "label", image.Label } };
                    var storeFields = new HashSet<string> { "label" };
                    var documentId = writeSession.Put(document, storeFields);

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
