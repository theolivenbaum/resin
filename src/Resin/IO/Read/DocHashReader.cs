using System;
using System.IO;

namespace Resin.IO.Read
{
    public class DocHashReader : IDisposable
    {
        private readonly Stream _stream;
        private long _position;

        public DocHashReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            _position = 0;
        }

        public UInt32 Read(int docId)
        {
            var distance = (docId*sizeof (UInt32)) - _position;

            if (distance < 0) throw new ArgumentOutOfRangeException("docId");

            if (distance > 0)
            {
                _stream.Seek(distance, SeekOrigin.Current);
            }

            _position = distance + sizeof (UInt32);

            var bytes = new byte[sizeof (UInt32)];

            _stream.Read(bytes, 0, sizeof (UInt32));

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}