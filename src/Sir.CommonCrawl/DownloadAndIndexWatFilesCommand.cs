using Microsoft.Extensions.Logging;
using Sir.Search;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Sir.CommonCrawl
{
    /// <summary>
    /// Download and index Common Crawl WAT files.
    /// </summary>
    /// <example>
    /// downloadandindexcommoncrawl --commonCrawlId CC-MAIN-2019-51 workingDirectory d:\ --collection cc_wat --skip 0 --take 1
    /// </example>
    public class DownloadAndIndexWatFilesCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger log)
        {
            var commonCrawlId = args["commonCrawlId"];
            var workingDirectory = args["workingDirectory"];
            var skip = int.Parse(args["skip"]);
            var take = int.Parse(args["take"]);
            var collectionName = args["collection"];

            var pathsFileName = $"{commonCrawlId}/wat.paths.gz";
            var localPathsFileName = Path.Combine(workingDirectory, pathsFileName);

            if (!File.Exists(localPathsFileName))
            {
                var url = $"https://commoncrawl.s3.amazonaws.com/crawl-data/{pathsFileName}";

                log.LogInformation($"downloading {url}");

                if (!Directory.Exists(Path.GetDirectoryName(localPathsFileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localPathsFileName));
                }

                using (var client = new WebClient())
                {
                    client.DownloadFile(url, localPathsFileName);
                }

                log.LogInformation($"downloaded {localPathsFileName}");
            }

            log.LogInformation($"processing {localPathsFileName}");

            Task writeTask = null;
            var took = 0;
            var skipped = 0;

            foreach (var watFileName in CCHelper.ReadAllLinesGromGz(localPathsFileName))
            {
                if (skip > skipped)
                {
                    skipped++;
                    continue;
                }

                if (took++ == take)
                {
                    break;
                }

                var localWatFileName = Path.Combine(workingDirectory, watFileName);

                if (!File.Exists(localWatFileName))
                {
                    var url = $"https://commoncrawl.s3.amazonaws.com/{watFileName}";

                    log.LogInformation($"downloading {url}");

                    if (!Directory.Exists(Path.GetDirectoryName(localWatFileName)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localWatFileName));
                    }

                    using (var client = new WebClient())
                    {
                        client.DownloadFile(url, localWatFileName);
                    }

                    log.LogInformation($"downloaded {localWatFileName}");
                }

                var refFileName = watFileName.Replace(".wat", "").Replace("/wat", "/warc");

                if (writeTask != null && !writeTask.IsCompleted)
                {
                    log.LogInformation($"awaiting write");

                    writeTask.Wait();
                }

                writeTask = Task.Run(() =>
                {
                    log.LogInformation($"processing {localWatFileName}");

                    CCHelper.WriteWatSegment(localWatFileName, collectionName, new TextModel(), log, refFileName);
                });
            }

            if (writeTask != null && !writeTask.IsCompleted)
            {
                log.LogInformation($"synchronizing write");

                writeTask.Wait();
            }
        }
    }
}
