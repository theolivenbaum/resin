using Microsoft.Extensions.Logging;
using Sir.Search;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sir.Mnist
{
    /// <summary>
    /// Test the accuracy of a MNIST index.
    /// </summary>
    /// <example>
    /// testmnist --imageFileName C:\temp\mnist\t10k-images.idx3-ubyte --labelFileName C:\temp\mnist\t10k-labels.idx1-ubyte --collection mnist
    /// </example>
    public class TestMnistCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var time = Stopwatch.StartNew();
            var images = new MnistReader(args["imageFileName"], args["labelFileName"]).Read();
            var collection = args["collection"];
            var count = 0;
            var errors = 0;
            var model = new ImageModel();

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), logger))
            using (var querySession = sessionFactory.CreateQuerySession(model))
            {
                var queryParser = new QueryParser<byte[][]>(sessionFactory, model, logger);

                foreach (var image in images)
                {
                    var query = queryParser.Parse(collection, image.Pixels, "image", "label", true, false);
                    var result = querySession.Query(query, 0, 1);

                    count++;

                    if (result.Total == 0)
                    {
                        errors++;
                    }

                    logger.LogInformation($"error rate: {errors / count}");
                }
            }

            logger.LogInformation($"tested {count} mnist images in {time.Elapsed}");
        }
    }
}