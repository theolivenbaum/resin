using System;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Read document meta-data (offset and length of document map) from the document index stream.
    /// </summary>
    public class DocIndexReader
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long) + sizeof(int);

        public int NumOfDocs
        {
            get
            {
                return ((int)_stream.Length / _blockSize) - 1;
            }
        }

        public DocIndexReader(Stream stream)
        {
            _stream = stream;
        }

        /// <summary>
        /// Get the offset and length of a document's key_id/value_id map.
        /// </summary>
        /// <param name="docId">Document ID</param>
        /// <returns>The offset and length of a document's key_id/value_id map</returns>
        public async Task<(long offset, int length)> ReadAsync(long docId)
        {
            var offs = docId * _blockSize;

            _stream.Seek(offs, SeekOrigin.Begin);

            var buf = new byte[_blockSize];
            var read = await _stream.ReadAsync(buf, 0, _blockSize);

            if (read == 0)
            {
                return (-1, -1); // return "nothing" if the docId has not yet been flushed.
            }

            return (BitConverter.ToInt64(buf, 0), BitConverter.ToInt32(buf, sizeof(long)));
        }

        public (long offset, int length) Read(long docId)
        {
            var offs = docId * _blockSize;

            _stream.Seek(offs, SeekOrigin.Begin);

            var buf = new byte[_blockSize];
            var read = _stream.Read(buf, 0, _blockSize);

            if (read == 0)
            {
                return (-1, -1); // return "nothing" if the docId has not yet been flushed.
            }

            return (BitConverter.ToInt64(buf, 0), BitConverter.ToInt32(buf, sizeof(long)));
        }
    }
}