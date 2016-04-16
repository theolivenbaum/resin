using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class DixFile : FileBase<DixFile>
    {
        // docid/file
        private readonly Dictionary<string, string>_docIdToFileIndex;

        public DixFile(string fileName) : base(fileName)        
        {
            _docIdToFileIndex = new Dictionary<string, string>();
        }

        public DixFile(string fileName, Dictionary<string, string> docIdToFileIndex) : base(fileName)
        {
            _docIdToFileIndex = docIdToFileIndex;
        }

        public Dictionary<string, string> DocIdToFileIndex
        {
            get { return _docIdToFileIndex; }
        }
    }
}