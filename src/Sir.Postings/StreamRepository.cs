using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace Sir.Postings
{
    public class StreamRepository
    {
        private readonly IConfigurationService _config;

        private IDictionary<ulong, IDictionary<long, IList<(long, long)>>> _index { get; set; }

        private const string DataFileNameFormat = "{0}.pos";
        private const string IndexFileName = "_.pix";

        public StreamRepository(IConfigurationService config)
        {
            _config = config;

            var ixfn = Path.Combine(_config.Get("data_dir"), IndexFileName);

            if (File.Exists(ixfn))
            {
                _index = ReadIndex(ixfn);
            }
            else
            {
                _index = new Dictionary<ulong, IDictionary<long, IList<(long, long)>>>();
            }
        }

        private IDictionary<ulong, IDictionary<long, IList<(long, long)>>> ReadIndex(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None, 4096))
            {
                var formatter = new BinaryFormatter();
                return (IDictionary<ulong, IDictionary<long, IList<(long, long)>>>)formatter.Deserialize(fs);
            }
        }

        private void FlushIndex()
        {
            var fileName = Path.Combine(_config.Get("data_dir"), IndexFileName);

            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(fs, _index);
            }
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

            var fileName = Path.Combine(_config.Get("data_dir"), string.Format(DataFileNameFormat, collectionId));
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

        public async Task<MemoryStream> Write(ulong collectionId, Stream request)
        {
            IDictionary<long, IList<(long, long)>> collectionIndex;

            if (!_index.TryGetValue(collectionId, out collectionIndex))
            {
                collectionIndex = new Dictionary<long, IList<(long, long)>>();
                _index.Add(collectionId, collectionIndex);
            }

            var mem = new MemoryStream();
            await request.CopyToAsync(mem);

            var messageBuf = mem.ToArray();
            int read = 0;

            // read first word of payload
            var payloadCount = BitConverter.ToInt32(messageBuf, 0);
            read = sizeof(int);

            // read lengths
            var lengths = new List<int>();

            for (int index = 0; index < payloadCount; index++)
            {
                lengths.Add(BitConverter.ToInt32(messageBuf, read));
                read += sizeof(int);
            }

            // read lists
            var lists = new List<byte[]>();

            for (int index = 0; index < lengths.Count; index++)
            {
                var size = lengths[index];
                var buf = new byte[size];
                Buffer.BlockCopy(messageBuf, read, buf, 0, size);
                lists.Add(buf);
            }

            var positions = new List<long>();

            var fileName = Path.Combine(_config.Get("data_dir"), string.Format(DataFileNameFormat, collectionId));

            using (var file = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true))
            foreach (var list in lists)
            {
                long pos = file.Position;

                await file.WriteAsync(list);

                long len = file.Position - pos;

                IList<(long, long)> ix;

                if (!collectionIndex.TryGetValue(pos, out ix))
                {
                    ix = new List<(long, long)>();

                    collectionIndex.Add(pos, ix);
                }

                ix.Add((pos, len));
                positions.Add(pos);
            }

            var res = new MemoryStream();

            for (int i = 0; i < positions.Count; i++)
            {
                await res.WriteAsync(BitConverter.GetBytes(positions[i]));
            }

            FlushIndex();

            return res;
        }
    }
}
