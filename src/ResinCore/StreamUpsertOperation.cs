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

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, Compression compression, string primaryKey, string fileName)
            : this(directory, analyzer, compression, primaryKey, File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
        {
        }

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, Compression compression, string primaryKey, Stream stream)
            : base(directory, analyzer, compression, primaryKey)
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