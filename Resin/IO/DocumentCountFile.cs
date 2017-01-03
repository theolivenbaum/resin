using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class DocumentCountFile : CompressedFileBase<DocumentCountFile>
    {
        private readonly Dictionary<string, int> _docCount;
        /// <summary>
        /// field/doc count
        /// </summary>
        public Dictionary<string, int> DocCount { get { return _docCount; } }

        public DocumentCountFile()
        {
            _docCount = new Dictionary<string,int>();
        }
    }
}