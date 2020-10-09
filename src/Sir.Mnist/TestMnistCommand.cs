using Microsoft.Extensions.Logging;
using Sir.Search;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
                    var successful = true;
                    var documentLabel = result.Total == 0 ? byte.MaxValue : (byte)result.Documents.First()["label"];

                    count++;

                    if (result.Total == 0 || documentLabel != image.Label)
                    {
                        errors++;
                        successful = false;
                    }

                    logger.LogInformation($"successful: {successful}. test label: {image.Label}. document label: {documentLabel}. {result.Total} hits. tot. errors: {errors}. total tests {count}. error rate: {(float)errors / count*100}%");
                }
            }

            logger.LogInformation($"tested {count} mnist images in {time.Elapsed}");
        }
    }
}