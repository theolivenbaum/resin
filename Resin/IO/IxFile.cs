using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class IxFile : CompressedFileBase<IxFile>
    {
        private readonly Dictionary<string, Dictionary<string, object>> _fields;
        /// <summary>
        /// field/docid/null
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Fields { get { return _fields; } }

        public IxFile()
        {
            _fields = new Dictionary<string, Dictionary<string, object>>();
        }
    }
}