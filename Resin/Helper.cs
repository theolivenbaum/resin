using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public static class Helper
    {
        /// <summary>
        /// Theo Lager's birthday. I love you.
        /// </summary>
        public static readonly DateTime BeginningOfTime = new DateTime(2007, 4, 23);

        private static readonly ILog Log = LogManager.GetLogger(typeof(Helper));

        public static IxFile Save(
            string dir, 
            string extensionIncDot, 
            DixFile dix, 
            FixFile fix, 
            Dictionary<string, Document> docs, 
            Dictionary<string, FieldFile> fieldFiles, 
            Dictionary<string, Trie> trieFiles)
        {
            var docWriter = new DocumentWriter(dir, docs);
            docWriter.Flush(dix);
            foreach (var fieldFile in fieldFiles)
            {
                var fileId = fieldFile.Key;
                fieldFile.Value.Save(Path.Combine(dir, fileId + ".f"));
                trieFiles[fileId].Save(Path.Combine(dir, fileId + ".f.tri"));
            }
            var fixFileId = Path.GetRandomFileName() + ".fix";
            var dixFileId = Path.GetRandomFileName() + ".dix";
            var fixFileName = Path.Combine(dir, fixFileId);
            var dixFileName = Path.Combine(dir, dixFileId);
            dix.Save(dixFileName);
            fix.Save(fixFileName);
            var ix = new IxFile(fixFileName, dixFileName, new List<Term>());
            var ixFileName = GenerateNewChronologicalFileName(dir, extensionIncDot);
            ix.Save(ixFileName);
            Log.InfoFormat("new {0} {1}", extensionIncDot == ".ix" ? "baseline" :  "commit", ixFileName);
            return ix;
        }

        public static string GetNextCommit(string fileName, IList<string> commits)
        {
            var latestBaseline = Int64.Parse(Path.GetFileNameWithoutExtension(fileName));
            foreach (var commitFileName in commits)
            {
                var timestamp = Int64.Parse(Path.GetFileNameWithoutExtension(commitFileName));
                if (timestamp <= latestBaseline) continue;
                return commitFileName;
            }
            return null;
        }

        public static string GetFileNameOfLatestIndex(string dir)
        {
            var files = GetFilesOrderedChronologically(dir, "*.ix");
            return files.LastOrDefault();
        }

        public static IEnumerable<string> GetFilesOrderedChronologically(string dir, string searchPattern)
        {
            var ids = Directory.GetFiles(dir, searchPattern)
                .Select(f => Int64.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(id => id);
            return ids.Select(id => Path.Combine(dir, id + searchPattern.Substring(1)));
        }

        public static string GenerateNewChronologicalFileName(string dir, string extensionIncDot)
        {
            var ticks = DateTime.Now.Ticks - BeginningOfTime.Ticks;
            var fileName = Path.Combine(dir, ticks + extensionIncDot);
            return fileName;
        }

        public static string GetResinDataDirectory()
        {
            var configPath = ConfigurationManager.AppSettings.Get("datadirectory");
            if (!string.IsNullOrWhiteSpace(configPath)) return configPath;
            string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Directory.GetParent(path).ToString();
            }
            return Path.Combine(path, "Resin");
        }

        /// <summary>
        /// Divides a list into batches.
        /// </summary>
        public static IEnumerable<IEnumerable<T>> IntoBatches<T>(this IEnumerable<T> list, int size)
        {
            if (size < 1)
            {
                yield return list;
            }
            else
            {
                var count = 0;
                var batch = new List<T>();
                foreach (var item in list)
                {
                    batch.Add(item);
                    if (size == ++count)
                    {
                        yield return batch;
                        batch = new List<T>();
                        count = 0;
                    }
                }
                if (batch.Count > 0) yield return batch;
            }
        }
    }
}