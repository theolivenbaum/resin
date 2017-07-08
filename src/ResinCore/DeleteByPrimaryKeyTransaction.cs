using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Resin.IO;
using Resin.Sys;
using DocumentTable;

namespace Resin
{
    public class DeleteByPrimaryKeyTransaction : IDeleteTransaction
    {
        private readonly string _directory;
        private readonly IEnumerable<string> _pks;
        private readonly List<BatchInfo> _ixs;

        public DeleteByPrimaryKeyTransaction(string directory, IEnumerable<string> primaryKeyValues)
        {
            _directory = directory;
            _ixs = Util.GetIndexFileNamesInChronologicalOrder(directory).Select(BatchInfo.Load).ToList();
            _pks = primaryKeyValues;
        }

        public void Commit()
        {
            var deleteSet = new LcrsTrie();

            foreach (var value in _pks)
            {
                var hashString = value.ToHash().ToString(CultureInfo.InvariantCulture);

                deleteSet.Add(hashString);
            }

            foreach (var ix in _ixs)
            {
                var docHashFileName = Path.Combine(_directory, string.Format("{0}.{1}", ix.VersionId, "pk"));
                var tmpDocHashFileName = Path.Combine(_directory, string.Format("{0}.{1}", ix.VersionId, "pk.tmp"));

                var tmpIxFileName = Path.Combine(_directory, ix.VersionId + ".ix.tmp");
                var ixFileName = Path.Combine(_directory, ix.VersionId + ".ix");

                var deleted = 0;

                using (var stream = new FileStream(tmpDocHashFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    foreach (var documentConfiguration in TableSerializer.DeserializeDocHashes(docHashFileName))
                    {
                        var hash = documentConfiguration.Hash.ToString(CultureInfo.InvariantCulture);

                        IList<Word> found = deleteSet.IsWord(hash).ToList();

                        var block = documentConfiguration;

                        if (found.Any())
                        {
                            if (!documentConfiguration.IsObsolete)
                            {
                                block = new DocHash(
                                    documentConfiguration.Hash, true);
                                deleted++;    
                            }
                        }

                        block.Serialize(stream);
                    }
                }               

                if (deleted > 0)
                {
                    ix.DocumentCount -= deleted;
                    ix.Serialize(tmpIxFileName);

                    File.Copy(tmpIxFileName, ixFileName, overwrite: true);
                    File.Copy(tmpDocHashFileName, docHashFileName, overwrite: true);

                    File.Delete(tmpIxFileName);
                    File.Delete(tmpDocHashFileName);
                }
            }
        }
    }
}