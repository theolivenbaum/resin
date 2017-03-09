using System;
using System.IO;
using CSharpTest.Net.Collections;

namespace Resin.IO.Write
{
    public class DbDocumentWriter : IDisposable
    {
        private readonly BPlusTree<int, byte[]> _db;

        public DbDocumentWriter(BPlusTree<int, byte[]> db)
        {
            _db = db;
        }

        public void Write(Document doc)
        {
            var bytes = Serialize(doc);

            _db.Add(doc.Id, bytes);
        }

        private byte[] Serialize(Document doc)
        {
            using (var ms = new MemoryStream())
            {
                GraphSerializer.Serializer.Serialize(ms, doc);
                var bytes = ms.ToArray();
                var compressed = QuickLZ.compress(bytes, 1);
                return compressed;
            }
        }

        public void Dispose()
        {
            _db.Dispose();
        }
    }
}