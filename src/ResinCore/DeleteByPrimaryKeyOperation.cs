using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Resin.IO;
using Resin.Sys;

namespace Resin
{
    public class DeleteByPrimaryKeyOperation
    {
        private readonly string _directory;
        private readonly IEnumerable<string> _values;
        private readonly List<IxInfo> _ixs;

        public DeleteByPrimaryKeyOperation(string directory, IEnumerable<string> values)
        {
            _directory = directory;
            _ixs = Util.GetIndexFileNamesInChronologicalOrder(directory).Select(IxInfo.Load).ToList();
            _values = values;
        }

        public void Commit()
        {
            var deleteSet = new LcrsTrie();

            foreach (var value in _values)
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
                    foreach (var document in Serializer.DeserializeDocHashes(docHashFileName))
                    {
                        Word found;

                        var hash = document.Hash.ToString(CultureInfo.InvariantCulture);

                        if (deleteSet.HasWord(hash, out found))
                        {
                            if (!document.IsObsolete)
                            {
                                document.IsObsolete = true;
                                deleted++;    
                            }
                        }

                        document.Serialize(stream);
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