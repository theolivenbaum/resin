using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Resin
{
    public static class Helper
    {
        public static readonly DateTime BeginningOfTime = new DateTime(2016, 4, 23);

        //private static readonly ILog Log = LogManager.GetLogger(typeof(Helper));

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

    internal static class WierdStringExtensions
    {
        public static string ToDocContainerId(this string docId)
        {
            if (string.IsNullOrEmpty(docId)) throw new ArgumentException("docId");
            var seed = docId.PadRight(3).Substring(0, 3);
            return seed.ToHash().ToString(CultureInfo.InvariantCulture);
        }

        public static string ToPostingsContainerId(this string field)
        {
            return field.ToHash().ToString(CultureInfo.InvariantCulture);
        }

        public static string ToTrieContainerId(this string field)
        {
            var fieldHash = field.ToHash().ToString(CultureInfo.InvariantCulture);
            return string.Format("{0}", fieldHash);
        }

        public static UInt64 ToHash(this string read)
        {
            UInt64 hashedValue = 3074457345618258791ul;
            for (int i = 0; i < read.Length; i++)
            {
                hashedValue += read[i];
                hashedValue *= 3074457345618258799ul;
            }
            return hashedValue;
        }
    }
}