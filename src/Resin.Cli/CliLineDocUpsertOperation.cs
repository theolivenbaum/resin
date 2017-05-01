using System.Collections.Generic;
using System.IO;
using Resin.Analysis;
using System.Linq;

namespace Resin.Cli
{
    public class CliLineDocUpsertOperation : StreamUpsertOperation
    {
        private readonly int _take;
        private readonly int _skip;

        public CliLineDocUpsertOperation(string directory, IAnalyzer analyzer, string fileName, int skip, int take, bool compression, string primaryKey)
            : base(directory, analyzer, fileName, compression, primaryKey)
        {
            _take = take;
            _skip = skip;
        }

        public CliLineDocUpsertOperation(string directory, IAnalyzer analyzer, Stream file, int skip, int take, bool compression, string primaryKey)
            : base(directory, analyzer, file, compression, primaryKey)
        {
            _take = take;
            _skip = skip;
        }

        protected IDictionary<string, string> Parse(string document)
        {
            var parts = document.Split(new[] { '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            return new KeyValuePair<string, string>[] {
                new KeyValuePair<string, string>("doctitle", parts[0]),
                new KeyValuePair<string, string>("body", parts[2]) }.ToDictionary(x => x.Key, x => x.Value);
        }

        protected override IEnumerable<Document> ReadSource()
        {
            Reader.ReadLine(); // first row is junk

            int skipped = 0;

            while (skipped++ < _skip)
            {
                Reader.ReadLine();
            }

            var took = 0;
            string line;

            while (true)
            {
                if ((line = Reader.ReadLine()) == null) break;
                if (took++ == _take) break;

                var doc = line.Substring(0, line.Length - 1);
                var dic = Parse(doc);

                yield return new Document(dic);
            }
        }
    }
}