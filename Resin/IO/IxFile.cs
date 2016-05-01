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

    [Serializable]
    public class IxInfo : CompressedFileBase<IxInfo>
    {
        private readonly Dictionary<string, int> _docCount;
        /// <summary>
        /// field/doc count
        /// </summary>
        public Dictionary<string, int> DocCount { get { return _docCount; } }

        public IxInfo()
        {
            _docCount = new Dictionary<string,int>();
        }
    }
}