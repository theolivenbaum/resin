using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Resin.IO;

namespace Tests
{
    public static class TestHelper
    {
        public static IEnumerable<Document> ToDocuments(this IEnumerable<IList<Field>> documents)
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