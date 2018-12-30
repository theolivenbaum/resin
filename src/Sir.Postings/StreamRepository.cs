using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Postings
{
    public class StreamRepository : IDisposable
    {
        private readonly IConfigurationProvider _config;
        private readonly StreamWriter _log;
        private const string DataFileNameFormat = "{0}.pos";

        public StreamRepository(IConfigurationProvider config)
        {
            _config = config;
            _log = Logging.CreateWriter("streamrepository");
        }

        public async Task<MemoryStream> Read(ulong collectionId, long id)
        {
            var timer = new Stopwatch();
            timer.Start();

            var result = new MemoryStream();

            var ixStream = GetReadableIndexStream(collectionId, id);

            if (ixStream == null)
            {
                return result;
            }

            var ix = new List<(long, long)>();

            using (ixStream)
            {
                var buf = new byte[16];
                int read;

                while ((read = await ixStream.ReadAsync(buf)) > 0)
                {
                    ix.Add((BitConverter.ToInt64(buf, 0), BitConverter.ToInt64(buf, sizeof(long))));
                }
            }

            using (var file = GetReadableDataStream(collectionId))
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

            _log.Log("read {0} bytes of postings data in {1}", result.Position, timer.Elapsed);

            return result;
        }

        public async Task<MemoryStream> Write(ulong collectionId, byte[] messageBuf)
        {
            int read = 0;

            // read first word of payload
            var payloadCount = BitConverter.ToInt32(messageBuf, 0);
            read = sizeof(int);

            // read lengths
            var lengths = new List<int>(payloadCount);

            for (int index = 0; index < payloadCount; index++)
            {
                var len = BitConverter.ToInt32(messageBuf, read);
                lengths.Add(len);
                read += sizeof(int);
            }

            // read lists
            var lists = new List<byte[]>(payloadCount);

            for (int index = 0; index < lengths.Count; index++)
            {
                var size = lengths[index];
                var buf = new byte[size];
                Buffer.BlockCopy(messageBuf, read, buf, 0, size);
                read += size;
                lists.Add(buf);
            }

            if (lists.Count != payloadCount)
            {
                throw new DataMisalignedException();
            }

            var positions = new List<long>(payloadCount);

            using (var file = GetWritableDataStream(collectionId))
            {
                for (int index = 0; index < lists.Count; index++)
                {
                    long pos = file.Position;
                    var word = lists[index];

                    file.Write(word);

                    long len = file.Position - pos;

                    if (len != lengths[index])
                    {
                        throw new DataMisalignedException();
                    }

                    using (var ixStream = GetWritableIndexStream(collectionId, pos))
                    {
                        ixStream.Write(BitConverter.GetBytes(pos));
                        ixStream.Write(BitConverter.GetBytes(len));
                    }

                    positions.Add(pos);
                }
            }

            if (positions.Count != payloadCount)
            {
                throw new DataMisalignedException();
            }

            var res = new MemoryStream();

            for (int i = 0; i < positions.Count; i++)
            {
                await res.WriteAsync(BitConverter.GetBytes(positions[i]));
            }

            res.Position = 0;

            return res;
        }

        private Stream GetWritableDataStream(ulong collectionId)
        {
            var fileName = Path.Combine(_config.Get("data_dir"), string.Format(DataFileNameFormat, collectionId));

            var stream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            return Stream.Synchronized(stream);
        }

        private Stream GetWritableIndexStream(ulong collectionId, long offset)
        {
            var fileName = Path.Combine(_config.Get("data_dir"), string.Format("{0}.{1}.pix", collectionId, offset));
            var stream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            return Stream.Synchronized(stream);
        }

        private Stream GetReadableIndexStream(ulong collectionId, long offset)
        {
            var fileName = Path.Combine(_config.Get("data_dir"), string.Format("{0}.{1}.pix", collectionId, offset));

            if (File.Exists(fileName))
            {
                var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
                return stream;
            }

            return null;
        }

        private Stream GetReadableDataStream(ulong collectionId)
        {
            var fileName = Path.Combine(_config.Get("data_dir"), string.Format(DataFileNameFormat, collectionId));

            if (File.Exists(fileName))
            {
                var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
                return stream;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
