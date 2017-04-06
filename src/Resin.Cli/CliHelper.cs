using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Resin.Cli
{
    public static class CliHelper
    {
        public static Stream ToStream(this List<Dictionary<string, string>> documents)
        {
            var json = new StringBuilder();
            json.AppendLine("[");

            foreach (var doc in documents)
            {
                json.AppendLine(JsonConvert.SerializeObject(doc, Formatting.None) + ",");
            }

            json.AppendLine("]");
            var jsonStr = json.ToString();
            return jsonStr.ToStream();
        }

        public static IEnumerable<Document> ToDocuments(this IEnumerable<IDictionary<string, string>> documents)
        {
            return documents.Select(doc => new Document(doc));
        }

        public static Stream ToStream(this string str)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
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