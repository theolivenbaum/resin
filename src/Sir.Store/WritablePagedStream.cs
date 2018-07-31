using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class WritablePagedStream : IDisposable
    {
        private static int PAGE_SIZE = 4096;
        private static int BLOCK_SIZE = sizeof(ulong);
        private static int SLOTS_PER_PAGE = PAGE_SIZE/BLOCK_SIZE;

        private readonly Stream _stream;
        private SortedList<long, byte[]> _pages;

        public WritablePagedStream(Stream stream)
        {
            _stream = stream;
            _pages = new SortedList<long, byte[]>();
        }

        public long AllocatePage()
        {
            _stream.Seek(0, SeekOrigin.End);

            var offset = _stream.Position;

            _stream.SetLength(offset + PAGE_SIZE);

            var page = new byte[PAGE_SIZE];

            _pages.Add(offset, page);

            return offset;
        }

        public void Append(long offset, ulong documentId)
        {
            var page = _pages[offset];
            
            ulong slot = 0;
            int pos = 0;

            while (pos < SLOTS_PER_PAGE)
            {
                slot = BitConverter.ToUInt64(page, (pos*sizeof(ulong)));

                if (slot == 0)
                {
                    break;
                }
                else
                {
                    pos++;
                }
            }

            if (pos == SLOTS_PER_PAGE)
            {
                // Page is full but luckily the last word is the offset for the next page.
                // Jump to a new page and append to it.

                long nextOffset = Convert.ToInt64(BitConverter.ToUInt64(page, PAGE_SIZE - BLOCK_SIZE));
                Append(nextOffset, documentId);
            }
            else if (pos == SLOTS_PER_PAGE - 2)
            {
                // Only two slots left in the page.
                // Fill them with a docId and the offset of the next page.

                var buf = BitConverter.GetBytes(documentId);
                Buffer.BlockCopy(buf, 0, page, pos * BLOCK_SIZE, BLOCK_SIZE);
                long nextOffset = AllocatePage();
                buf = BitConverter.GetBytes(nextOffset);
                Buffer.BlockCopy(buf, 0, page, pos * BLOCK_SIZE, BLOCK_SIZE);
            }
            else
            {
                // There was a vacant slot in the page.

                var buf = BitConverter.GetBytes(documentId);
                Buffer.BlockCopy(buf, 0, page, pos * BLOCK_SIZE, BLOCK_SIZE);
            }
        }

        public void Dispose()
        {
            _stream.Seek(_pages.Keys[0], SeekOrigin.Begin);

            foreach(var offset in _pages.Keys)
            {
                var page = _pages[offset];
                _stream.Write(page, 0, page.Length);
            }
        }
    }
}
