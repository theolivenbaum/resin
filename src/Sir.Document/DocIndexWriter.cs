using System;
using System.IO;

namespace Sir.Documents
{
    /// <summary>
    /// Stores offset and length of document map to a stream.
    /// </summary>
    public class DocIndexWriter :IDisposable
    {
        private readonly Stream _stream;
        public static int BlockSize = sizeof(long)+sizeof(int);

        public DocIndexWriter(Stream stream)
        {
            _stream = stream;
        }

        public void Flush()
        {
            _stream.Flush();
        }

        /// <summary>
        /// Get the next auto-incrementing doc id
        /// </summary>
        /// <returns>The next auto-incrementing doc id</returns>
        public long IncrementDocId()
        {
            var id = _stream.Length / BlockSize;

            _stream.SetLength(_stream.Length+BlockSize);

            return id;
        }

        /// <summary>
        /// Add offset and length of doc map to index
        /// </summary>
        /// <param name="offset">offset of doc map</param>
        /// <param name="len">length of doc map</param>
        public void Put(long docId, long offset, int len)
        {
            _stream.Seek(BlockSize * docId, SeekOrigin.Begin);
            _stream.Write(BitConverter.GetBytes(offset));
            _stream.Write(BitConverter.GetBytes(len));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
