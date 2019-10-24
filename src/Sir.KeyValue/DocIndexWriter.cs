using System;
using System.IO;

namespace Sir.KeyValue
{
    /// <summary>
    /// Write offset and length of document map to the document index stream.
    /// </summary>
    public class DocIndexWriter :IDisposable
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

        public void Flush()
        {
            _stream.Flush();
        }

        /// <summary>
        /// Get the next auto-incrementing doc id
        /// </summary>
        /// <returns>The next auto-incrementing doc id</returns>
        public long PeekNextDocId()
        {
            return _stream.Position / BlockSize;
        }

        /// <summary>
        /// Add offset and length of doc map to index
        /// </summary>
        /// <param name="offset">offset of doc map</param>
        /// <param name="len">length of doc map</param>
        public void Put(long offset, int len)
        {
            _stream.Write(BitConverter.GetBytes(offset));
            _stream.Write(BitConverter.GetBytes(len));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
