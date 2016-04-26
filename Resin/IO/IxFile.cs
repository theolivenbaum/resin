using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class IxFile : FileBase<IxFile>
    {
        private readonly Dictionary<string, string> _fileIds;
        /// <summary>
        /// id/fileId (IO-safe)
        /// </summary>
        public Dictionary<string, string> FileIds { get { return _fileIds; } }

        private readonly Dictionary<string, string> _docContainers;
        /// <summary>
        ///  docid.field/containerid
        /// </summary>
        public Dictionary<string, string> DocContainers { get { return _docContainers; } }

        private readonly Dictionary<string, Dictionary<string, object>> _fields;
        /// <summary>
        /// field/docid/null
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Fields { get { return _fields; } }

        public IxFile()
        {
            _fileIds = new Dictionary<string, string>();
            _docContainers = new Dictionary<string, string>();
            _fields = new Dictionary<string, Dictionary<string, object>>();
        }


    }
}