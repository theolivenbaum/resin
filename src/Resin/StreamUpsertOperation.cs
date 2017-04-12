using System.IO;
using System.Text;
using Resin.Analysis;

namespace Resin
{
    public abstract class StreamUpsertOperation : UpsertOperation
    {
        protected readonly StreamReader Reader;
        protected readonly int Take;

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, string jsonFileName, int take = int.MaxValue)
            : this(directory, analyzer, File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None), take)
        {
        }

        protected StreamUpsertOperation(string directory, IAnalyzer analyzer, Stream jsonFile, int take = int.MaxValue)
            : base(directory, analyzer)
        {
            Take = take;

            var bs = new BufferedStream(jsonFile);

            Reader = new StreamReader(bs, Encoding.UTF8);
        }
    }
}