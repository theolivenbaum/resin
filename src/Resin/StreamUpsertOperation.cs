using System.IO;
using System.Text;
using Resin.Analysis;

namespace Resin
{
    public abstract class StreamUpsertOperation : UpsertOperation
    {
        protected readonly StreamReader Reader;

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName, bool compression)
            : this(directory, analyzer, File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None), compression)
        {
        }

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile, bool compression)
            : base(directory, analyzer, compression)
        {

            var bs = new BufferedStream(jsonFile);

            Reader = new StreamReader(bs, Encoding.UTF8);
        }
    }
}