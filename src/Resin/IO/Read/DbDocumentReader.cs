using System;
using System.IO;
using CSharpTest.Net.Collections;

namespace Resin.IO.Read
{
    public class DbDocumentReader : IDisposable
    {
        private readonly BPlusTree<int, byte[]> _db;

        public DbDocumentReader(BPlusTree<int, byte[]> db)
        {
            _db = db;
        }

        public Document Get(int docId)
        {
            var bytes = _db[docId];
            var decompressed = QuickLZ.decompress(bytes);
            return (Document)GraphSerializer.Serializer.Deserialize(new MemoryStream(decompressed));
        }

        public void Dispose()
        {
            _db.Dispose();
        }
    }
}