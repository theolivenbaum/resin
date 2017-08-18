using log4net;
using StreamIndex;
using System.Collections.Generic;
using System.Diagnostics;

namespace Resin.IO
{
    public abstract class PostingsReader
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(PostingsReader));

        public abstract IList<DocumentPosting> ReadPositionsFromStream(BlockInfo address);
        public abstract IList<DocumentPosting> ReadTermCountsFromStream(BlockInfo address);

        public IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses)
        {
            var time = Stopwatch.StartNew();
            var result = new List<DocumentPosting>();

            foreach (var address in addresses)
            {
                result.AddRange(ReadTermCountsFromStream(address));
            }

            Log.DebugFormat("read {0} postings in {1}", result.Count, time.Elapsed);

            return result;
        }

        public IList<IList<DocumentPosting>> ReadPositions(IList<IList<BlockInfo>> addresses)
        {
            var time = Stopwatch.StartNew();
            var lists = new List<IList<DocumentPosting>>();

            foreach (var list in addresses)
            {
                foreach (var address in list)
                {
                    lists.Add(ReadPositionsFromStream(address));
                }
            }

            lists.Sort(new MostSignificantPostingsListsComparer());

            Log.InfoFormat("created a postings matrix with width {0} in {1}", lists.Count, time.Elapsed);

            return lists;
        }
    }
}
