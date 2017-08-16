using DocumentTable;
using StreamIndex;
using System;
using System.IO;
using System.Linq;

namespace Resin
{
    public class FullTextReadSessionFactory : IReadSessionFactory, IDisposable
    {
        private readonly string _directory;
        private readonly FileStream _compoundFile;

        public FullTextReadSessionFactory(string directory, int bufferSize = 4096*12)
        {
            _directory = directory;

            var version = Directory.GetFiles(directory, "*.ix")
                .Select(f => long.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(v => v).First();

            var compoundFileName = Path.Combine(_directory, version + ".rdb");

            _compoundFile = new FileStream(
                compoundFileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.RandomAccess);
        }

        public IReadSession OpenReadSession(long version)
        {
            var ix = FullTextSegmentInfo.Load(Path.Combine(_directory, version + ".ix"));

            return OpenReadSession(ix);
        }

        public IReadSession OpenReadSession(SegmentInfo ix)
        {
            return new FullTextReadSession(
                ix,
                new DocHashReader(_compoundFile, ix.DocHashOffset),
                new BlockInfoReader(_compoundFile, ix.DocAddressesOffset),
                _compoundFile);
        }

        public void Dispose()
        {
            _compoundFile.Dispose();
        }
    }
}
