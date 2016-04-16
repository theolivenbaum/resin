using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace Resin
{
    public static class Helper
    {
        public static readonly DateTime BeginningOfTime = new DateTime(2016, 4, 1);

        public static IEnumerable<string> GetIndexFiles(string dir)
        {
            var ids = Directory.GetFiles(dir, "*.ix")
                .Select(f => Int64.Parse(Path.GetFileNameWithoutExtension(f) ?? "-1"))
                .OrderBy(id => id);
            return ids.Select(id => Path.Combine(dir, id + ".ix"));
        }

        public static string GetChronologicalFileId(string dir)
        {
            var ticks = DateTime.Now.Ticks - BeginningOfTime.Ticks;
            var fileName = Path.Combine(dir, ticks + ".ix");
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