using System.Collections.Generic;
using System.IO;
using System.Text;
using Resin.Analysis;

namespace Resin
{
    public abstract class StreamWriteOperation : Writer
    {
        protected abstract IDictionary<string, string> Parse(string document);
 
        private readonly StreamReader _reader;
        private readonly int _take;

        protected StreamWriteOperation(string directory, IAnalyzer analyzer, string jsonFileName, int take = int.MaxValue)
            : this(directory, analyzer, File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None), take)
        {
        }

        protected StreamWriteOperation(string directory, IAnalyzer analyzer, Stream jsonFile, int take = int.MaxValue)
            : base(directory, analyzer)
        {
            _take = take;

            var bs = new BufferedStream(jsonFile);

            _reader = new StreamReader(bs, Encoding.UTF8);
        }

        protected override IEnumerable<Document> ReadSource()
        {
            var line = _reader.ReadLine(); // first row is "["
            var took = 0;

            while ((line = _reader.ReadLine()) != null)
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