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
            return await _data.Write(collectionId, (MemoryStream)payload);
        }

        public async void Append(ulong collectionId, long id, Stream payload)
        {
            await _data.Write(collectionId, (MemoryStream)payload, id);
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

    public class StreamRepository
    {
        private IDictionary<ulong, IDictionary<long, IList<(long, long)>>> _index { get; set; }

        private const string FileNameFormat = "{0}.pos";

        public StreamRepository()
        {
            _index = new Dictionary<ulong, IDictionary<long, IList<(long, long)>>>();
        }

        public async Task<MemoryStream> Read(ulong collectionId, long id)
        {
            IDictionary<long, IList<(long, long)>> collectionIndex;

            if (!_index.TryGetValue(collectionId, out collectionIndex))
            {
                throw new ArgumentException(nameof(collectionId));
            }

            IList<(long, long)> ix;

            if (!collectionIndex.TryGetValue(id, out ix))
            {
                throw new ArgumentException(nameof(id));
            }

            var fileName = string.Format(FileNameFormat, collectionId);
            var result = new MemoryStream();

            using (var file = new FileStream(
                fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
            {
                foreach (var loc in ix)
                {
                    var pos = loc.Item1;
                    var len = loc.Item2;

                    file.Seek(pos, SeekOrigin.Begin);

                    var buf = new byte[len];
                    var read = await file.ReadAsync(buf);

                    if (read != len)
                    {
                        throw new InvalidDataException();
                    }

                    await result.WriteAsync(buf);
                }
            }

            return result;
        }

        public async Task<long> Write(ulong collectionId, MemoryStream payload)
        {
            IDictionary<long, IList<(long, long)>> collectionIndex;

            if (!_index.TryGetValue(collectionId, out collectionIndex))
            {
                collectionIndex = new Dictionary<long, IList<(long, long)>>();
                _index.Add(collectionId, collectionIndex);
            }

            var buf = payload.ToArray();
            var fileName = string.Format(FileNameFormat, collectionId);
            long pos;
            long len;

            using (var file = new FileStream(
                fileName, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true))
            {
                pos = file.Position;

                await file.WriteAsync(buf);

                len = file.Position - pos;
            }


            IList<(long, long)> ix;

            if (!collectionIndex.TryGetValue(pos, out ix))
            {
                ix = new List<(long, long)>();

                collectionIndex.Add(pos, ix);
            }

            ix.Add((pos, len));

            return pos;
        }

        public async Task<long> Write(ulong collectionId, MemoryStream payload, long id)
        {
            IDictionary<long, IList<(long, long)>> collectionIndex;

            if (!_index.TryGetValue(collectionId, out collectionIndex))
            {
                collectionIndex = new Dictionary<long, IList<(long, long)>>();
                _index.Add(collectionId, collectionIndex);
            }

            IList<(long, long)> ix;

            if (!collectionIndex.TryGetValue(id, out ix))
            {
                throw new ArgumentException(nameof(id));
            }

            var buf = payload.ToArray();
            var fileName = string.Format(FileNameFormat, collectionId);
            long pos;
            long len;

            using (var file = new FileStream(
                fileName, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true))
            {
                pos = file.Position;

                await file.WriteAsync(buf);

                len = file.Position - pos;
            }

            ix.Add((pos, len));

            return pos;
        }
    }
}
