using System.IO;
using System.Text;
using Resin.Analysis;

namespace Resin
{
    public abstract class StreamUpsertOperation : UpsertOperation
    {
        protected readonly StreamReader Reader;

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName)
            : this(directory, analyzer, File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None))
        {
        }

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile)
            : base(directory, analyzer)
        {

            var bs = new BufferedStream(jsonFile);

            Reader = new StreamReader(bs, Encoding.UTF8);
        }
    }
}