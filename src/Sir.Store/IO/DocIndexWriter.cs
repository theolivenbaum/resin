using System;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Write offset and length of document map to the document index stream.
    /// </summary>
    public class DocIndexWriter
    {
        private readonly Stream _stream;
        public static int BlockSize = sizeof(long)+sizeof(int);

        public DocIndexWriter(Stream stream)
        {
            _stream = stream;

            if (_stream.Length == 0)
            {
                _stream.SetLength(BlockSize);
                _stream.Seek(0, SeekOrigin.End);
            }
        }

        /// <summary>
        /// Get the next auto-incrementing doc id
        /// </summary>
        /// <returns>The next auto-incrementing doc id</returns>
        private long GetNextDocId()
        {
            return _stream.Position / BlockSize;
        }

        /// <summary>
        /// Add offset and length of doc map to index
        /// </summary>
        /// <param name="offset">offset of doc map</param>
        /// <param name="len">length of doc map</param>
        public async Task<long> Append(long offset, int len)
        {
            var id = GetNextDocId();

            await _stream.WriteAsync(BitConverter.GetBytes(offset), 0, sizeof(long));
            await _stream.WriteAsync(BitConverter.GetBytes(len), 0, sizeof(int));

            return id;
        }
    }
}
