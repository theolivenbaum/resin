using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Resin;

namespace Tests
{
    public static class TestHelper
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
    }
}