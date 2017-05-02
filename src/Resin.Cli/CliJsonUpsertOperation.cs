using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Resin.Analysis;
using System;
using Resin.IO;

namespace Resin.Cli
{
    public class CliJsonUpsertOperation : StreamUpsertOperation
    {
        private readonly int _take;
        private readonly int _skip;

        public CliJsonUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName, int skip, int take, bool compression, string primaryKey)
            : base(directory, analyzer, jsonFileName, compression, primaryKey)
        {
            _take = take;
            _skip = skip;
        }

        public CliJsonUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile, int skip, int take, bool compression, string primaryKey) 
            : base(directory, analyzer, jsonFile, compression, primaryKey)
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
            var cursorPos = Console.CursorLeft;

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

                Console.SetCursorPosition(cursorPos, Console.CursorTop);
                Console.Write(took);
            }
            Console.WriteLine("");
        }
    }
}