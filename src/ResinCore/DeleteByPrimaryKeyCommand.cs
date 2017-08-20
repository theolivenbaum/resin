using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.Sys;
using Resin.Documents;

namespace Resin
{
    public class DeleteByPrimaryKeyCommand : IDeleteTransaction
    {
        private readonly string _directory;
        private readonly IEnumerable<string> _pks;
        private readonly IDictionary<long, SegmentInfo> _ixs;

        public DeleteByPrimaryKeyCommand(string directory, IEnumerable<string> primaryKeyValues)
        {
            _directory = directory;
            _ixs = Util.GetIndexVersionInfoInChronologicalOrder(directory);
            _pks = primaryKeyValues;
        }

        public void Execute()
        {
            var deleteSet = new HashSet<ulong>(
                _pks.Select(x => x.ToHash()).ToList());

            foreach (var ix in _ixs)
            {
                var dataFile = Path.Combine(_directory, ix.Key + ".rdb");

                using (var stream = new FileStream(dataFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    stream.Seek(ix.Value.DocHashOffset, SeekOrigin.Begin);

                    var buffer = new byte[DocumentSerializer.SizeOfDocHash()];

                    while (stream.Position < ix.Value.DocAddressesOffset)
                    {
                        stream.Read(buffer, 0, buffer.Length);

                        var hash = DocumentSerializer.DeserializeDocHash(buffer);

                        if (deleteSet.Contains(hash.Hash))
                        {
                            stream.Position = stream.Position - buffer.Length;
                            buffer[0] = 1;
                            stream.Write(buffer, 0, buffer.Length);
                            deleteSet.Remove(hash.Hash);
                        }
                        if (deleteSet.Count == 0) break;
                    }
                }
            }
        }
    }
}