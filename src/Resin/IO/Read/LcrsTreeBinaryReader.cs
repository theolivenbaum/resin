using System;
using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Read
{
    public class LcrsTreeBinaryReader : IDisposable
    {
        private readonly StreamReader _sr;

        public LcrsTreeBinaryReader(StreamReader sr)
        {
            _sr = sr;
        }

        public IEnumerable<LcrsTrie> Read()
        {
            string data;

            while ((data = _sr.ReadLine()) != null)
            {
                var bytes = Convert.FromBase64String(data);
                using (var memStream = new MemoryStream(bytes))
                {
                    var firstLevelChild = Deserialize(memStream);
                    var root = new LcrsTrie('\0', false);
                    root.LeftChild = firstLevelChild;
                    yield return root;
                }
            }
        }

        private LcrsTrie Deserialize(Stream stream)
        {
            return (LcrsTrie)GraphSerializer.Serializer.Deserialize(stream);
        }

        public void Dispose()
        {
            if (_sr != null)
            {
                _sr.Dispose();
            }
        }
    }
}