using System;
using System.Collections.Generic;

namespace Resin.IO
{
    /// <summary>
    /// Document index containing pointers to all documents in the index or commit.
    /// </summary>
    [Serializable]
    public class DixFile : FileBase<DixFile>
    {
        // docid/file
        private readonly Dictionary<string, string>_docIdToFileId;

        public DixFile()       
        {
            _docIdToFileId = new Dictionary<string, string>();
        }

        public DixFile(Dictionary<string, string> docIdToFileId)
        {
            _docIdToFileId = docIdToFileId;
        }

        public Dictionary<string, string> DocIdToFileId
        {
            get { return _docIdToFileId; }
        }
    }
}