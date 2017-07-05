using System;
using System.IO;

namespace DocumentTable
{
    public class DocumentInfoReader : IDisposable
    {
        private readonly Stream _stream;

        public DocumentInfoReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public DocumentInfo Read(int docId)
        {
            var distance = (docId*TableSerializer.SizeOfDocHash()) - _stream.Position;

            if (distance < 0)
            {
                distance = (docId * TableSerializer.SizeOfDocHash());

                _stream.Seek(distance, SeekOrigin.Begin);
            }
            else
            {
                _stream.Seek(distance, SeekOrigin.Current);
            }

            var hash = TableSerializer.DeserializeDocHash(_stream).Value;

            return hash;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}