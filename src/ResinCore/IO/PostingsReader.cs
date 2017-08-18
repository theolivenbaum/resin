using log4net;
using StreamIndex;
using System.Collections.Generic;
using System.Diagnostics;

namespace Resin.IO
{
    public abstract class PostingsReader
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(PostingsReader));

        public abstract IList<DocumentPosting> ReadPositionsFromStream(IList<BlockInfo> addresses);
        public abstract IList<DocumentPosting> ReadTermCountsFromStream(IList<BlockInfo> addresses);

        public IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses)
        {
            var time = Stopwatch.StartNew();
            var result = ReadTermCountsFromStream(addresses);

            Log.DebugFormat("read {0} term counts in {1}", result.Count, time.Elapsed);

            return result;
        }

        public IList<IList<DocumentPosting>> ReadPositions(IList<IList<BlockInfo>> addresses)
        {
            var time = Stopwatch.StartNew();
            var lists = new List<IList<DocumentPosting>>();

            foreach (var list in addresses)
            {
                lists.Add(ReadPositionsFromStream(list));
            }

            lists.Sort(new MostSignificantPostingsListsComparer());

            Log.InfoFormat("created a postings matrix with width {0} in {1}", lists.Count, time.Elapsed);

            return lists;
        }
    }
}
