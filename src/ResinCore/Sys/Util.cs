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
        private static Int64 GetTicks()
        {
            return DateTime.Now.Ticks;
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

        private static IEnumerable<string> GetIndexFileNames(string directory)
        {
            //TODO: check for a lock file
            return Directory.GetFiles(directory, "*.ix");
        }

        public static IEnumerable<string> GetIndexFileNamesInChronologicalOrder(string directory)
        {
            return GetIndexFileNames(directory)
                .Select(f => new {id = long.Parse(Path.GetFileNameWithoutExtension(f)), fileName = f})
                .OrderBy(info => info.id)
                .Select(info => info.fileName);
        }

        public static int GetDocumentCount(string directory)
        {
            return GetIndexFileNamesInChronologicalOrder(directory)
                .Select(BatchInfo.Load)
                .Sum(x=>x.DocumentCount);   
        }

        public static int GetDocumentCount(IEnumerable<BatchInfo> ixs)
        {
            return ixs.Sum(x => x.DocumentCount);
        }

        /// <summary>
        /// Divides one big workload into many smaller workloads.
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

        public static void RemoveAll(string ixFileName)
        {
            File.Delete(ixFileName);

            var dir = Path.GetDirectoryName(ixFileName);
            var searchPattern = Path.GetFileNameWithoutExtension(ixFileName) + "*";
            var files = Directory.GetFiles(dir, searchPattern);

            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
    }
}