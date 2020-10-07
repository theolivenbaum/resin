using Microsoft.Extensions.Logging;
using Sir.Search;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Sir.Mnist
{
    public class MnistUtilPlugin : IUtilPlugin
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var time = Stopwatch.StartNew();
            var collectionId = "mnist".ToHash();
            var images = new MnistReader(args["imageFileName"], args["labelFileName"]).Read().Select(x => x.Pixels);
            var documentId = 1;

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), logger))
            using (var indexSession = sessionFactory.CreateIndexSession(collectionId, new StreamModel()))
            {
                foreach (var image in images)
                {
                    indexSession.Put(documentId++, 0, image);
                }
            }

            logger.LogInformation($"indexed {documentId - 1} mnist images");
        }
    }
}
