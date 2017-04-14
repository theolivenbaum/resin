using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Resin.Analysis;

namespace Resin.Cli
{
    public class CliUpsertOperation : StreamUpsertOperation
    {
        private readonly int _take;
        private readonly int _skip;

        public CliUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName, int skip, int take) : base(directory, analyzer, jsonFileName)
        {
            _take = take;
            _skip = skip;
        }

        public CliUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile, int skip, int take) : base(directory, analyzer, jsonFile)
        {
            _take = take;
            _skip = skip;
        }

        protected IDictionary<string, string> Parse(string document)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(document);
        }

        protected override IEnumerable<Document> ReadSource()
        {
            Reader.ReadLine(); // first row is "["

            int skipped = 0;

            while (skipped++ < _skip)
            {
                Reader.ReadLine();
            }

            var took = 0;
            string line;

            while ((line = Reader.ReadLine()) != null)
            {
                if (line[0] == ']') break;

                if (took++ == _take) break;

                var json = line.Substring(0, line.Length - 1);
                var dic = Parse(json);

                yield return new Document(dic);
            }
        }
    }
}