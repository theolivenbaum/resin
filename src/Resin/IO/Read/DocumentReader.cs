using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO.Write;

namespace Resin.IO.Read
{
    public class DocumentReader : IDisposable
    {
        private readonly Stream _stream;
        private long _position;

        public DocumentReader(Stream stream)
        {
            _stream = stream;
            _position = 0;
        }

        public IEnumerable<Document> Get(IEnumerable<BlockInfo> blocks)
        {
            return blocks.Select(Get);
        }

        private Document Get(BlockInfo info)
        {
            var distance = info.Position - _position;
            
            if (distance > 0)
            {
                _stream.Seek(distance, SeekOrigin.Current);
            }

            var buffer = new byte[info.Length];

            _stream.Read(buffer, 0, buffer.Length);

            _position = info.Position + info.Length;
            
            return Deserialize(buffer);
        }

        private Document Deserialize(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return (Document)GraphSerializer.Serializer.Deserialize(stream);
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}