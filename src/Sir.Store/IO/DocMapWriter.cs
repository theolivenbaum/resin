using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Sir.Store
{
    /// <summary>
    /// Write document maps (key_id/val_id) to the document map stream.
    /// </summary>
    public class DocMapWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly Semaphore _writeSync;

        public DocMapWriter(Stream stream)
        {
            _stream = stream;

            bool createdSystemWideSem;

            _writeSync = new Semaphore(1, 2, "Sir.Store.DocMapWriter", out createdSystemWideSem);

            if (!createdSystemWideSem)
            {
                _writeSync.Dispose();
                _writeSync = Semaphore.OpenExisting("Sir.Store.DocMapWriter");
            }
        }

        public (long offset, int length) Append(IList<(long keyId, long valId)> doc)
        {
            _writeSync.WaitOne();

            var off = _stream.Position;

            foreach (var kv in doc)
            {
                _stream.Write(BitConverter.GetBytes(kv.keyId));
                _stream.Write(BitConverter.GetBytes(kv.valId));
            }

            _writeSync.Release();

            return (off, sizeof(long) * 2 * doc.Count);
        }

        public void Dispose()
        {
            _writeSync.Dispose();
        }
    }
}
