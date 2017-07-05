using DocumentTable;
using System;
using System.IO;

namespace DocumentTable
{
    public class DocumentInfoReader : IDisposable
    {
        private readonly Stream _stream;
        private long _position;

        public DocumentInfoReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            _position = 0;
        }

        public DocumentInfo Read(int docId)
        {
            var distance = (docId*TableSerializer.SizeOfDocHash()) - _position;

            if (distance < 0)
            {
                _position = 0;

                distance = (docId * TableSerializer.SizeOfDocHash()) - _position;

                _stream.Seek(distance, SeekOrigin.Begin);
            }
            else
            {
                _stream.Seek(distance, SeekOrigin.Current);
            }

            var hash = TableSerializer.DeserializeDocHash(_stream).Value;

            _position += distance+ TableSerializer.SizeOfDocHash();

            return hash;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}