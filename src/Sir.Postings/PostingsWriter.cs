using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Postings
{
    public class PostingsWriter : IWriter
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;

        public PostingsWriter(StreamRepository data)
        {
            _data = data;
        }

        public async Task<long> Write(ulong collectionId, Stream payload)
        {
            return await _data.Write(payload);
        }

        public async void Append(ulong collectionId, long id, Stream payload)
        {
            await _data.Write(payload);
        }

        public void Dispose()
        {
        }
    }

    public class PostingsReader : IReader
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;

        public PostingsReader(StreamRepository data)
        {
            _data = data;
        }

        public Result Read(Query Query)
        {
            var documentId = Query.Collection;

            IList<(long, long)> ix;

            if (_data.Index.TryGetValue(documentId, out ix))
            {
                var postings = ReadFromFile(ix);

                return new Result { Data = postings, Total = postings.Count };
            }

            return new Result { Total = 0 };
        }

        public IList<ulong> ReadFromFile(IList<(long, long)> ix)
        {
            var result = new List<ulong>();

            foreach(var pos in ix)
            {
                var buf = new byte[pos.Item2];
                _data.
            }
        }

        public void Dispose()
        {
        }
    }

    public class StreamRepository : IDisposable
    {
        public IDictionary<ulong, IList<(long, long)>> Index { get; set; }
        public Stream Data { get; set; }

        private const string FileName = "postings.pos";

        public StreamRepository()
        {
            Index = new Dictionary<ulong, IList<(long, long)>>();
            Data = new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
        }

        public async Task<long> Write(Stream payload)
        {
            var mem = new MemoryStream();

            await payload.CopyToAsync(mem);

            var buf = mem.ToArray();
            var pos = Data.Position;

            await Data.WriteAsync(buf);

            var len = Data.Position - pos;
            IList<(long, long)> ix;

            if (!Index.TryGetValue(pos, out ix))
            {
                ix = new List<(long, long)>();

                Index.Add(id, ix);
            }

            ix.Add((pos, len));

            return pos;
        }


        public async Task<long> Write(long id, Stream payload)
        {
            var mem = new MemoryStream();

            await payload.CopyToAsync(mem);

            var buf = mem.ToArray();
            var pos = Data.Position;

            await Data.WriteAsync(buf);

            var len = Data.Position - pos;
            IList<(long, long)> ix;

            if (!Index.TryGetValue(id, out ix))
            {
                ix = new List<(long, long)>();

                Index.Add(id, ix);
            }

            ix.Add((pos, len));

            return Convert.ToUInt64(pos);
        }

        public void Dispose()
        {
            if (Data != null)
                Data.Dispose();
        }
    }
}
