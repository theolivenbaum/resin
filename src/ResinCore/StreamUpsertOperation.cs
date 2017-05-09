using System;
using System.IO;
using System.Text;
using Resin.Analysis;
using Resin.IO;

namespace Resin
{
    public abstract class StreamUpsertOperation : UpsertOperation, IDisposable
    {
        protected readonly StreamReader Reader;

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, string fileName, Compression compression)
            : this(directory, analyzer, File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None), compression)
        {
        }

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, Stream stream, Compression compression)
            : base(directory, analyzer, compression)
        {

            var bs = new BufferedStream(stream);

            Reader = new StreamReader(bs, Encoding.UTF8);
        }

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}