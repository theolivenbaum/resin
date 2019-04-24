using System;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Write offset and length of document map to the document index store.
    /// </summary>
    public class DocIndexWriter
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long)+sizeof(int);
        private static object _crossDomainSync = new object();

        public DocIndexWriter(Stream stream)
        {
            _stream = stream;

            if (_stream.Length == 0)
            {
                _stream.SetLength(_blockSize);
                _stream.Seek(0, SeekOrigin.End);
            }
        }

        /// <summary>
        /// Get the next auto-incrementing doc id (peeking is allowed)
        /// </summary>
        /// <returns>The next auto-incrementing doc id</returns>
        private long GetNextDocId()
        {
            return _stream.Position / _blockSize;
        }

        /// <summary>
        /// Add offset and length of doc map to index
        /// </summary>
        /// <param name="len">length of doc map</param>
        public long Append(int len)
        {
            lock (_crossDomainSync)
            {
                var offset = GetNextDocId();

                _stream.Write(BitConverter.GetBytes(offset), 0, sizeof(long));
                _stream.Write(BitConverter.GetBytes(len), 0, sizeof(int));

                return offset;
            }
        }
    }
}
