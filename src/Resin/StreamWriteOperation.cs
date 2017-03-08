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

        public StreamWriteOperation(string directory, IAnalyzer analyzer, string jsonFileName, int take)
            : this(directory, analyzer, File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None), take)
        {
        }

        public StreamWriteOperation(string directory, IAnalyzer analyzer, Stream jsonFile, int take) : base(directory, analyzer)
        {
            _take = take;

            var bs = new BufferedStream(jsonFile);
            _reader = new StreamReader(bs, Encoding.Unicode);
        }

        protected override IEnumerable<Document> ReadSource()
        {
            _reader.ReadLine();

            string line;
            var took = 0;

            while ((line = _reader.ReadLine()) != null)
            {
                if (line[0] == ']') break;

                if (took++ == _take) break;

                var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(line.Substring(0, line.Length - 1));

                yield return new Document(dic);
            }
        }

        public override void Dispose()
        {
            if (_reader != null)
            {
                _reader.Close();
                _reader.Dispose();
            }
            base.Dispose();
        }
    }
}