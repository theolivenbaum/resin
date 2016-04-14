using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class FixFile : FileBase<FixFile>
    {
        // field/fileid
        private readonly Dictionary<string, string> _fieldIndex;

        public FixFile()
        {
            _fieldIndex = new Dictionary<string, string>();
        }

        public Dictionary<string, string> FieldIndex
        {
            get { return _fieldIndex; }
        }
    }
}