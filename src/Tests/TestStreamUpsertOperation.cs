using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Resin;
using Resin.Analysis;

namespace Tests
{
    public class TestStreamUpsertOperation : StreamUpsertOperation
    {
        public TestStreamUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName, int take = Int32.MaxValue)
            : base(directory, analyzer, jsonFileName, take)
        {
        }

        public TestStreamUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile, int take = Int32.MaxValue)
            : base(directory, analyzer, jsonFile, take)
        {
        }

        protected IDictionary<string, string> Parse(string document)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(document);
        }

        protected override IEnumerable<Document> ReadSource()
        {
            var line = Reader.ReadLine(); // first row is "["
            var took = 0;

            while ((line = Reader.ReadLine()) != null)
            {
                if (line[0] == ']') break;

                if (took++ == Take) break;

                var json = line.Substring(0, line.Length - 1);
                var dic = Parse(json);

                yield return new Document(dic);
            }
        }
    }
}