using StreamIndex;
using System;
using System.IO;

namespace Resin.IO
{
    public class PostingsWriter
    {
        public Stream Stream { get; private set; }

        public PostingsWriter(Stream stream)
        {
            Stream = stream;
        }

        public BlockInfo Write(Stream postings)
        {
            postings.Position = 0;
            var pos = Stream.Position;
            postings.CopyTo(Stream);
            int size = Convert.ToInt32(Stream.Position - pos);
            postings.Dispose();
            return new BlockInfo(pos, size);
        }
    }
}