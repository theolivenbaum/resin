using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Resin;
using Resin.Analysis;
using Resin.IO;

namespace Tests
{
    public class TestStreamUpsertOperation : StreamUpsertOperation
    {
        public TestStreamUpsertOperation(string directory, IAnalyzer analyzer, string fileName)
            : base(directory, analyzer, fileName, false, null)
        {
        }

        public TestStreamUpsertOperation(string directory, IAnalyzer analyzer, Stream file)
            : base(directory, analyzer, file, false, null)
        {
        }

        protected IDictionary<string, string> Parse(string document)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(document);
        }

        protected override IEnumerable<Document> ReadSource()
        {
            Reader.ReadLine(); // first row is "["
            string line;
            while ((line = Reader.ReadLine()) != null)
            {
                if (line[0] == ']') break;

                var json = line.Substring(0, line.Length - 1);
                var dic = Parse(json);

                yield return new Document(dic);
            }
        }
    }
}