using System;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Write document meta-data (offset and length of document map) to the document index stream.
    /// </summary>
    public class DocIndexWriter
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long)+sizeof(int);

        public DocIndexWriter(Stream stream)
        {
            _stream = stream;

            if (_stream.Position == 0)
            {
                // Don't emmit zero as docid as that is the signal for "no data" for the postings reader.
                _stream.Write(new byte[_blockSize], 0, _blockSize);
            }
        }

        /// <summary>
        /// Get the next auto-incrementing doc id (peeking is allowed)
        /// </summary>
        /// <returns>The next auto-incrementing doc id</returns>
        public ulong GetNextDocId()
        {
            return Convert.ToUInt64(_stream.Position) / Convert.ToUInt64(_blockSize);
        }

        /// <summary>
        /// Add offset and length of doc map to index
        /// </summary>
        /// <param name="offset">offset of doc map</param>
        /// <param name="len">length of doc map</param>
        public void Append(long offset, int len)
        {
            _stream.Write(BitConverter.GetBytes(offset), 0, sizeof(long));
            _stream.Write(BitConverter.GetBytes(len), 0, sizeof(int));
        }
    }
}
