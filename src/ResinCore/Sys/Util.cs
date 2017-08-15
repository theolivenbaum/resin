using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;
using DocumentTable;

namespace Resin.Sys
{
    public static class Util
    {
        public static string ReplaceOrAppend(this string input, int index, char newChar)
        {
            var chars = input.ToCharArray();
            if (index == input.Length) return input + newChar;
            chars[index] = newChar;
            return new string(chars);
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