using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class DixFile : FileBase<DixFile>
    {
        // docid/file
        private readonly Dictionary<string, string>_docIdToFileIndex;

        public DixFile()
        {
            _docIdToFileIndex = new Dictionary<string, string>();
        }

        public DixFile(Dictionary<string, string> docIdToFileIndex)
        {
            _docIdToFileIndex = docIdToFileIndex;
        }

        public Dictionary<string, string> DocIdToFileIndex
        {
            get { return _docIdToFileIndex; }
        }
    }
}