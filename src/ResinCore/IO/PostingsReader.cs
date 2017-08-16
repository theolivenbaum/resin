using log4net;
using StreamIndex;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Resin.IO
{
    public class PostingsReader
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(PostingsReader));

        private readonly Stream _stream;
        private readonly long _offset;

        public PostingsReader(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
        }

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

        public IList<IList<DocumentPosting>> ReadMany(IList<IList<BlockInfo>> addresses)
        {
            var time = Stopwatch.StartNew();
            var lists = new List<IList<DocumentPosting>>();

            foreach(var list in addresses)
            {
                foreach(var address in list)
                {
                    lists.Add(ReadFromStream(address));
                }
            }

            lists.Sort(new PostingsListsComparer());

            Log.InfoFormat("created a postings matrix with width {0} in {1}", lists.Count, time.Elapsed);

            return lists;
        }

        public IList<DocumentPosting> Read(IList<BlockInfo> addresses)
        {
            var time = Stopwatch.StartNew();
            var result = new List<DocumentPosting>();

            foreach (var address in addresses)
            {
                result.AddRange(ReadFromStream(address));
            }

            Log.DebugFormat("read {0} postings in {1}", result.Count, time.Elapsed);
            return result;
        }

        private IList<DocumentPosting> ReadFromStream(BlockInfo address)
        {
            _stream.Seek(_offset + address.Position, SeekOrigin.Begin);

            return Serializer.DeserializePostings(_stream, address.Length);
        }

        private IList<DocumentPosting> ReadTermCountsFromStream(BlockInfo address)
        {
            _stream.Seek(_offset + address.Position, SeekOrigin.Begin);

            return Serializer.DeserializeTermCounts(_stream, address.Length);
        }
    }
}