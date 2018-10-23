using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Postings
{
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
