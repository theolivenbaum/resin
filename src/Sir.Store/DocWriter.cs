using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Write document maps (key_id/val_id) from the document map stream.
    /// </summary>
    public class DocWriter
    {
        private readonly Stream _stream;

        public DocWriter(Stream stream)
        {
            _stream = stream;
        }

        public (long offset, int length) Append(IList<(long keyId, long valId)> doc)
        {
            var off = _stream.Position;
            var len = 0;

            foreach (var kv in doc)
            {
                _stream.Write(BitConverter.GetBytes(kv.keyId), 0, sizeof(long));
                _stream.Write(BitConverter.GetBytes(kv.valId), 0, sizeof(long));
                len += sizeof(long) * 2;
            }
            
            return (off, len);
        }
    }
}
