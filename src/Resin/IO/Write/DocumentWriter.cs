using System;
using System.IO;

namespace Resin.IO.Write
{
    public class DocumentWriter : IDisposable
    {
        private long _position;
        private readonly Stream _stream;

        public DocumentWriter(Stream stream)
        {
            _position = 0;
            _stream = stream;
        }

        public BlockInfo Write(Document doc)
        {
            var bytes = Serialize(doc);
            var info = new BlockInfo(_position, bytes.Length);

            _stream.Write(bytes, 0, bytes.Length);
            _position += bytes.Length;

            return info;
        }

        private byte[] Serialize(Document doc)
        {
            using (var ms = new MemoryStream())
            {
                GraphSerializer.Serializer.Serialize(ms, doc);
                return ms.ToArray();
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }

    public struct BlockInfo
    {
        public long Position;
        public int Length;

        public BlockInfo(long position, int length)
        {
            Position = position;
            Length = length;
        }
    }
}