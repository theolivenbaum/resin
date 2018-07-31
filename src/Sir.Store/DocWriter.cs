using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class DocWriter
    {
        private readonly Stream _stream;

        public DocWriter(Stream stream)
        {
            _stream = stream;
        }

        public (long offset, int length) Append(IList<(uint keyId, uint valId)> doc)
        {
            var off = _stream.Position;
            var len = 0;
            foreach (var kv in doc)
            {
                _stream.Write(BitConverter.GetBytes(kv.keyId), 0, sizeof(uint));
                _stream.Write(BitConverter.GetBytes(kv.valId), 0, sizeof(uint));
                len += sizeof(uint) * 2;
            }
            
            return (off, len);
        }
    }
}
