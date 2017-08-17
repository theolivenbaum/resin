using StreamIndex;
using System;
using System.IO;
using System.Linq;

namespace DocumentTable
{
    public class ReadSessionFactory : IReadSessionFactory, IDisposable
    {
        private readonly string _directory;
        private readonly FileStream _data;

        public ReadSessionFactory(string directory, int bufferSize = 4096 * 12)
        {
            _directory = directory;

            var version = Directory.GetFiles(directory, "*.ix")
                .Select(f => long.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(v => v).First();

            var dataFn = Path.Combine(_directory, version + ".rdb");

            _data = new FileStream(
                dataFn,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.RandomAccess);
        }

        public IReadSession OpenReadSession(long version)
        {
            var ix = SegmentInfo.Load(Path.Combine(_directory, version + ".ix"));

            return OpenReadSession(ix);
        }

        public IReadSession OpenReadSession(SegmentInfo ix)
        {
            return new ReadSession(
                ix,
                new DocHashReader(_data, ix.DocHashOffset),
                new BlockInfoReader(_data, ix.DocAddressesOffset),
                _data);
        }

        public void Dispose()
        {
            _data.Dispose();
        }
    }
}
