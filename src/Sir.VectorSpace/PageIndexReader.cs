﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.VectorSpace
{
    /// <summary>
    /// Index segment address reader.
    /// </summary>
    public class PageIndexReader : IDisposable
    {
        private readonly Stream _stream;

        public PageIndexReader(Stream stream)
        {
            _stream = stream;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public (long offset, long length) ReadAt(long offset)
        {
            _stream.Seek(offset, SeekOrigin.Begin);

            Span<byte> buf = stackalloc byte[sizeof(long) * 2];

            var read = _stream.Read(buf);

            if (read < sizeof(long) * 2)
                throw new DataMisalignedException();

            var list = MemoryMarshal.Cast<byte, long>(buf);

            return (list[0], list[1]);
        }

        public (long offset, long length) Get(int id)
        {
            return ReadAt(id * sizeof(long) * 2);
        }

        public IList<(long offset, long length)> ReadAll()
        {
            var mem = new MemoryStream();
            _stream.CopyTo(mem);
            var buf = mem.ToArray();

            var read = 0;
            var result = new List<(long, long)>();

            while (read < buf.Length)
            {
                var offset = BitConverter.ToInt64(buf, read);

                read += sizeof(long);

                var length = BitConverter.ToInt64(buf, read);

                read += sizeof(long);

                result.Add((offset, length));
            }

            return result;
        }
    }
}