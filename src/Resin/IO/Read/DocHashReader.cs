using System;
using System.IO;

namespace Resin.IO.Read
{
    public class DocHashReader : IDisposable
    {
        private readonly Stream _stream;
        private long _position;

        public DocHashReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            _position = 0;
        }

        public DocHash Read(int docId)
        {
            var distance = (docId*Serializer.SizeOfDocHash()) - _position;

            if (distance < 0) throw new ArgumentOutOfRangeException("docId");

            if (distance > 0)
            {
                _stream.Seek(distance, SeekOrigin.Current);
            }

            var doc = Serializer.DeserializeDocHash(_stream);

            _position += distance+Serializer.SizeOfDocHash();

            return doc;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}