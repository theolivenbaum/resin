using System;
using System.Collections.Generic;

namespace Resin.IO
{
    /// <summary>
    /// A mapping between field names and a file IDs.
    /// </summary>
    [Serializable]
    public class FixFile : FileBase<FixFile>
    {
        // field/fileid
        private readonly Dictionary<string, string> _fieldToFileId;

        public FixFile()   
        {
            _fieldToFileId = new Dictionary<string, string>();
        }

        public Dictionary<string, string> FieldToFileId
        {
            get { return _fieldToFileId; }
        }
    }
}