using System;
using System.IO;
using System.Text;

namespace Resin.IO.Write
{
    public class LcrsTreeBinaryWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        public static object SyncRoot = new object();

        public LcrsTreeBinaryWriter(StreamWriter writer)
        {
            _writer = writer;
        }

        public void Write(LcrsTrie node)
        {
            var bytes = BuildStringRepresentation(node);

            if (bytes.Length == 0) throw new Exception();

            var base64 = Convert.ToBase64String(bytes);

            _writer.WriteLine(base64);
        }

        private byte[] BuildStringRepresentation(LcrsTrie node)
        {
            var sb = new StringBuilder();
            node.SerializeDepthFirst(sb, 0);
            return Encoding.Unicode.GetBytes(sb.ToString());
        }

        //private byte[] Serialize(string node)
        //{
        //    using (var stream = new MemoryStream())
        //    {
        //        BinaryFile.Serializer.Serialize(stream, node);
        //        return stream.ToArray();
        //    }
        //}

        //private byte[] Serialize(LcrsTrie node)
        //{
        //    using (var stream = new MemoryStream())
        //    {
        //        BinaryFile.Serializer.Serialize(stream, node);
        //        return stream.ToArray();
        //    }
        //}

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Dispose();
            }
        }
    }
}