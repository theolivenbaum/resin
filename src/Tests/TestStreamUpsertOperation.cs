using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Resin;
using Resin.Analysis;

namespace Tests
{
    public class TestStreamUpsertOperation : StreamUpsertOperation
    {
        public TestStreamUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName)
            : base(directory, analyzer, jsonFileName, false, "_id")
        {
        }

        public TestStreamUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile)
            : base(directory, analyzer, jsonFile, false, "_id")
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