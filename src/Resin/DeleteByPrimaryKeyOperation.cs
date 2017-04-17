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
                var hashString = value.ToHashString();

                deleteSet.Add(hashString);
            }

            foreach (var ix in _ixs)
            {
                var docHashFileName = Path.Combine(_directory, string.Format("{0}.{1}", ix.VersionId, "dhs.tmp"));
                var deleted = 0;

                using (var stream = new FileStream(docHashFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    foreach (var document in Serializer.DeserializeDocHashes(docHashFileName))
                    {
                        Word found;

                        if (deleteSet.HasWord(document.Hash.ToString(CultureInfo.InvariantCulture), out found))
                        {
                            document.IsObsolete = true;
                            deleted++;
                        }

                        document.Serialize(stream);
                    }
                }

                ix.DocumentCount -= deleted;

                ix.Serialize(Path.Combine(_directory, ix.VersionId + ".ix.tmp"));
            }
        }
    }
}