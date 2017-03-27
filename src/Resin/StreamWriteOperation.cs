using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Resin.Analysis;

namespace Resin
{
    public class StreamWriteOperation : Writer
    {
        private readonly StreamReader _reader;
        private readonly int _take;

        public StreamWriteOperation(string directory, IAnalyzer analyzer, string jsonFileName, int take = int.MaxValue)
            : this(directory, analyzer, File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None), take)
        {
        }

        public StreamWriteOperation(string directory, IAnalyzer analyzer, Stream jsonFile, int take = int.MaxValue)
            : base(directory, analyzer)
        {
            _take = take;

            var bs = new BufferedStream(jsonFile);

            _reader = new StreamReader(bs, Encoding.Unicode);
        }

        protected override IEnumerable<Document> ReadSource()
        {
// ReSharper disable once RedundantAssignment
            var line = _reader.ReadLine(); // intentional (first row is "[\n")
            var took = 0;

            while ((line = _reader.ReadLine()) != null)
            {
                if (line[0] == ']') break;

                if (took++ == _take) break;

                var json = line.Substring(0, line.Length - 1);

                var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                yield return new Document(dic);
            }
        }
    }
}