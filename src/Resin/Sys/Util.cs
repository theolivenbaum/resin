using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Resin.IO;
using Resin.IO.Read;

namespace Resin.Sys
{
    public static class Util
    {
        public static readonly DateTime BeginningOfTime = new DateTime(2016, 4, 23);

        public static long GetChronologicalFileId()
        {
            var ticks = DateTime.Now.Ticks - BeginningOfTime.Ticks;
            return ticks;
        }

        public static string GetDataDirectory()
        {
            var configPath = ConfigurationManager.AppSettings.Get("resin.datadirectory");
            if (!string.IsNullOrWhiteSpace(configPath)) return configPath;
            string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Directory.GetParent(path).ToString();
            }
            return Path.Combine(path, "Resin");
        }

        private static IEnumerable<string> GetIndexFileNames(string directory)
        {
            return Directory.GetFiles(directory, "*.ix");
        }

        public static IEnumerable<string> GetIndexFileNamesInChronologicalOrder(string directory)
        {
            return GetIndexFileNames(directory)
                .Select(f => new {id = long.Parse(new FileInfo(f).Name.Replace(".ix", "")), fileName = f})
                .OrderBy(info => info.id)
                .Select(info => info.fileName);
        }

        public static IDictionary<string, int> GetDocumentCount(IEnumerable<IxInfo> ixs)
        {
            var documentCount = new Dictionary<string, int>();

            foreach (var x in ixs)
            {
                foreach (var field in x.DocumentCount)
                {
                    if (documentCount.ContainsKey(field.Key))
                    {
                        documentCount[field.Key] += field.Value;
                    }
                    else
                    {
                        documentCount[field.Key] = field.Value;
                    }
                }
            }
            return documentCount;
        }

        public static IEnumerable<IList<DocumentPosting>> ReadPostings(string directory, IxInfo ix, IEnumerable<Term> terms)
        {
            var posFileName = Path.Combine(directory, string.Format("{0}.{1}", ix.VersionId, "pos"));

            using (var reader = new PostingsReader(new FileStream(posFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan)))
            {
                var addresses = terms.Select(term => term.Word.PostingsAddress.Value).OrderBy(adr => adr.Position).ToList();

                yield return reader.Get(addresses).SelectMany(x => x).ToList();
            }
        }
    }
}