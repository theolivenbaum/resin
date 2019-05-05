using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Write document maps (key_id/val_id) to the document map stream.
    /// </summary>
    public class DocMapWriter
    {
        private readonly Stream _stream;

        public DocMapWriter(Stream stream)
        {
            _stream = stream;
        }

        public (long offset, int length) Append(IList<(long keyId, long valId)> doc)
        {
            var off = _stream.Position;
            var len = 0;

            foreach (var kv in doc)
            {
                _stream.Write(BitConverter.GetBytes(kv.keyId));
                _stream.Write(BitConverter.GetBytes(kv.valId));

                len += sizeof(long) * 2;
            }

            return (off, len);
        }

        public async Task<(long offset, int length)> AppendAsync(IList<(long keyId, long valId)> doc)
        {
            var off = _stream.Position;
            var len = 0;

            foreach (var kv in doc)
            {
                await _stream.WriteAsync(BitConverter.GetBytes(kv.keyId));
                await _stream.WriteAsync(BitConverter.GetBytes(kv.valId));

                len += sizeof(long) * 2;
            }
            
            return (off, len);
        }
    }
}
