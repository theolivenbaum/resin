using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;
using DocumentTable;

namespace Resin.Sys
{
    public static class Util
    {
        private static readonly Random Rnd;
        private static long Ticks;

        static Util()
        {
            Rnd = new Random();
            Ticks = DateTime.Now.Ticks;
        }

        private static Int64 GetTicks()
        {
            var count = Rnd.Next(1, 4);
            for (int i = 0; i < count; i++)
            {
                if (Rnd.Next(1, 6).Equals(count)) break;
            }
            return Ticks++;
        }

        public static string ReplaceOrAppend(this string input, int index, char newChar)
        {
            var chars = input.ToCharArray();
            if (index == input.Length) return input + newChar;
            chars[index] = newChar;
            return new string(chars);
        }

        public static long GetNextChronologicalFileId()
        {
            return GetTicks();
        }

        public static string[] GetIndexFileNamesInChronologicalOrder(string directory)
        {
            return Directory.GetFiles(directory, "*.ix");
        }

        public static IList<SegmentInfo> GetIndexVersionListInChronologicalOrder(string directory)
        {
            var list = new List<SegmentInfo>();
            foreach (var file in Directory.GetFiles(directory, "*.ix"))
            {
                var version = long.Parse(Path.GetFileNameWithoutExtension(file));
                var ix = SegmentInfo.Load(Path.Combine(directory, version + ".ix"));
                list.Add(ix);
            }
            return list;
        }

        public static IDictionary<long, SegmentInfo> GetIndexVersionInfoInChronologicalOrder(string directory)
        {
            var list = new Dictionary<long, SegmentInfo>();
            foreach (var file in Directory.GetFiles(directory, "*.ix"))
            {
                var version = long.Parse(Path.GetFileNameWithoutExtension(file));
                var ix = SegmentInfo.Load(Path.Combine(directory, version + ".ix"));
                list.Add(version, ix);
            }
            return list;
        }

        public static long[] GetIndexVersionsInChronologicalOrder(string directory)
        {
            var list = new List<long>();
            foreach(var file in Directory.GetFiles(directory, "*.ix"))
            {
                list.Add(long.Parse(Path.GetFileNameWithoutExtension(file)));
            }
            return list.ToArray();
        }

        public static IEnumerable<string> GetDataFileNamesInChronologicalOrder(string directory)
        {
            return Directory.GetFiles(directory, "*.rdb")
                .Select(f => new { id = long.Parse(Path.GetFileNameWithoutExtension(f)), fileName = f })
                .OrderBy(info => info.id)
                .Select(info => info.fileName);
        }

        public static int GetDocumentCount(string directory, out int numOfSegments)
        {
            var total = 0;
            numOfSegments = 0;
            foreach (var segment in GetIndexVersionListInChronologicalOrder(directory))
            {
                numOfSegments++;
                total += segment.DocumentCount;
            }
            return total;
        }

        public static int GetDocumentCount(IEnumerable<SegmentInfo> ixs)
        {
            return ixs.Sum(x => x.DocumentCount);
        }

        /// <summary>
        /// Divides one big workload into a number of smaller workloads with set size.
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

        public static IDictionary<string, object> ToDictionary(dynamic obj)
        {
            var dictionary = new Dictionary<string, object>();
            foreach (var propertyInfo in obj.GetType().GetProperties())
                if (propertyInfo.CanRead && propertyInfo.GetIndexParameters().Length == 0)
                    dictionary[propertyInfo.Name] = propertyInfo.GetValue(obj, null);
            return dictionary;
        }

        public static bool TryAquireWriteLock(string directory, out FileStream lockFile)
        {
            lockFile = null;

            var fileName = Path.Combine(directory, "write.lock");
            try
            {
                lockFile = new FileStream(
                                fileName, FileMode.CreateNew, FileAccess.Write,
                                FileShare.None, 4, FileOptions.DeleteOnClose);
                return true;
            }
            catch (IOException)
            {
                if (lockFile != null)
                {
                    lockFile.Dispose();
                    lockFile = null;
                }
                return false;
            } 
        }

        public static bool IsSegmented(string ixFileName)
        {
            var dir = Path.GetDirectoryName(ixFileName);
            var searchPattern = Path.GetFileNameWithoutExtension(ixFileName) + "*";
            var files = Directory.GetFiles(dir, searchPattern);

            foreach (var file in files.Where(f => Path.GetExtension(f) == ".six"))
            {
                var segs = Serializer.DeserializeLongList(file).ToList();
                if (segs.Count > 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}