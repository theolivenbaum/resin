using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Resin.Sys;

namespace Resin.IO.Read
{
    public class LcrsTreeBinaryReader : IDisposable
    {
        private readonly StreamReader _sr;

        public LcrsTreeBinaryReader(StreamReader sr)
        {
            _sr = sr;
        }

        public IEnumerable<LcrsTreeReader> Read()
        {
            string data;

            while ((data = _sr.ReadLine()) != null)
            {
                var bytes = Convert.FromBase64String(data);
                var str = Encoding.Unicode.GetString(bytes);
                var stream = Helper.GenerateStreamFromString(str);
                yield return new LcrsTreeReader(new StreamReader(stream));
            }
        }

        //private LcrsTrie Deserialize(Stream stream)
        //{
        //    return (LcrsTrie)BinaryFile.Serializer.Deserialize(stream);
        //}

        public void Dispose()
        {
            if (_sr != null)
            {
                _sr.Dispose();
            }
        }
    }
}