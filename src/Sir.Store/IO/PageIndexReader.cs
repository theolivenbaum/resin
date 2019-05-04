using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Index bitmap segment reader.
    /// </summary>
    public class PageIndexReader
    {
        private readonly Stream _stream;

        public PageIndexReader(Stream stream)
        {
            _stream = stream;
        }

        public async Task<IList<(long offset, long length)>> ReadAllAsync()
        {
            var mem = new MemoryStream();
            await _stream.CopyToAsync(mem);
            var buf = mem.ToArray();

            var read = 0;
            var result = new List<(long, long)>();

            while (read < buf.Length)
            {
                var offset = BitConverter.ToInt64(buf, read);

                read += sizeof(long);

                var length = BitConverter.ToInt64(buf, read);

                read += sizeof(long);

                result.Add((offset, length));
            }

            return result;
        }

        public IList<(long offset, long length)> ReadAll()
        {
            var mem = new MemoryStream();
            _stream.CopyTo(mem);
            var buf = mem.ToArray();

            var read = 0;
            var result = new List<(long, long)>();

            while (read < buf.Length)
            {
                var offset = BitConverter.ToInt64(buf, read);

                read += sizeof(long);

                var length = BitConverter.ToInt64(buf, read);

                read += sizeof(long);

                result.Add((offset, length));
            }

            return result;
        }
    }
}