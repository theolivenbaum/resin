using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using Resin.IO;

namespace Resin.Sys
{
    public static class Helper
    {
        public static readonly DateTime BeginningOfTime = new DateTime(2016, 4, 23);

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

        public static string GetDataDirectory()
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
        public static string ToPostingsFileId(this Term term)
        {
            if (term == null) throw new ArgumentNullException("term");

            var val = term.Word.Value.PadRight(1).Substring(0, 1);
            return val.ToHash().ToString(CultureInfo.InvariantCulture);
        }

        public static string ToDocFileId(this string docId)
        {
            if (string.IsNullOrEmpty(docId)) throw new ArgumentException("docId");

            var val = docId.PadRight(3).Substring(0, 3);
            return val.ToHash().ToString(CultureInfo.InvariantCulture);
        }

        public static string ToTrieFileId(this string field)
        {
            var fieldHash = field.ToHash().ToString(CultureInfo.InvariantCulture);
            return string.Format("{0}", fieldHash);
        }

        /// <summary>
        /// Knuth hash. http://stackoverflow.com/questions/9545619/a-fast-hash-function-for-string-in-c-sharp
        /// One could also use https://msdn.microsoft.com/en-us/library/system.security.cryptography.sha1.aspx
        /// </summary>
        /// <param name="read"></param>
        /// <returns></returns>
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