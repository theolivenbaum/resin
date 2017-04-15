using System;
using System.IO;
using System.Text;
using Resin.Analysis;

namespace Resin
{
    public abstract class StreamUpsertOperation : UpsertOperation, IDisposable
    {
        protected readonly StreamReader Reader;

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName, bool compression, string primaryKey)
            : this(directory, analyzer, File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None), compression, primaryKey)
        {
        }

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile, bool compression, string primaryKey)
            : base(directory, analyzer, compression, primaryKey)
        {

            var bs = new BufferedStream(jsonFile);

            Reader = new StreamReader(bs, Encoding.UTF8);
        }

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}