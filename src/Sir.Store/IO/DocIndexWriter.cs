using System;
using System.IO;
using System.Threading;

namespace Sir.Store
{
    /// <summary>
    /// Write offset and length of document map to the document index stream.
    /// </summary>
    public class DocIndexWriter :IDisposable
    {
        private readonly Stream _stream;
        public static int BlockSize = sizeof(long)+sizeof(int);
        private readonly Semaphore _writeSync;

        public DocIndexWriter(Stream stream)
        {
            _stream = stream;

            bool createdSystemWideSem;

            _writeSync = new Semaphore(1, 2, "Sir.Store.DocIndexWriter", out createdSystemWideSem);

            if (!createdSystemWideSem)
            {
                _writeSync.Dispose();
                _writeSync = Semaphore.OpenExisting("Sir.Store.DocIndexWriter");
            }

            _writeSync.WaitOne();

            if (_stream.Length == 0)
            {
                _stream.SetLength(BlockSize);
                _stream.Seek(0, SeekOrigin.End);
            }

            _writeSync.Release();
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
        public long Append(long offset, int len)
        {
            _writeSync.WaitOne();

            var id = GetNextDocId();

            _stream.Write(BitConverter.GetBytes(offset));
            _stream.Write(BitConverter.GetBytes(len));

            _writeSync.Release();

            return id;
        }

        public void Dispose()
        {
            _writeSync.Dispose();
        }
    }
}
