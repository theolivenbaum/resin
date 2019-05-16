using System;
using System.IO;
using System.Threading;

namespace Sir.Store
{
    /// <summary>
    /// Store the location of a value.
    /// </summary>
    public class ValueIndexWriter : IDisposable
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long) + sizeof(int) + sizeof(byte);
        private readonly Semaphore _writeSync;

        public ValueIndexWriter(Stream stream)
        {
            _stream = stream;

            bool createdSystemWideSem;

            _writeSync = new Semaphore(1, 2, "Sir.Store.ValueIndexWriter", out createdSystemWideSem);

            if (!createdSystemWideSem)
            {
                _writeSync.Dispose();
                _writeSync = Semaphore.OpenExisting("Sir.Store.ValueIndexWriter");
            }
        }

        public long Append(long offset, int len, byte dataType)
        {
            _writeSync.WaitOne();

            var position = _stream.Position;

            _stream.Write(BitConverter.GetBytes(offset));
            _stream.Write(BitConverter.GetBytes(len));
            _stream.WriteByte(dataType);

            _writeSync.Release();

            return position == 0 ? 0 : position / _blockSize;
        }

        public void Dispose()
        {
            _writeSync.Dispose();
        }
    }
}
