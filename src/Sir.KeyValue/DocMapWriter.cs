using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.KeyValue
{
    /// <summary>
    /// Write document maps (key_id/val_id) to the document map stream.
    /// </summary>
    public class DocMapWriter : IDisposable
    {
        private readonly Stream _stream;

        public DocMapWriter(Stream stream)
        {
            _stream = stream;
        }

        public void Flush()
        {
            _stream.Flush();
        }

        public (long offset, int length) Put(IList<(long keyId, long valId)> doc)
        {
            if (doc.Count == 0)
                throw new ArgumentException(nameof(doc));

            var off = _stream.Position;

            foreach (var kv in doc)
            {
                _stream.Write(BitConverter.GetBytes(kv.keyId));
                _stream.Write(BitConverter.GetBytes(kv.valId));
            }

            return (off, sizeof(long) * 2 * doc.Count);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
