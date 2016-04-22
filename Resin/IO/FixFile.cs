using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class FixFile : FileBase<FixFile>
    {
        // field/fileid
        private readonly Dictionary<string, string> _fields;
        private readonly Dictionary<string, string> _fileIds;

        public Dictionary<string, string> Fields { get { return _fields; } }
        public Dictionary<string, string> FileIds { get { return _fileIds; } }

        public FixFile()
        {
            _fields = new Dictionary<string, string>();
            _fileIds = new Dictionary<string, string>();
        }

        public void Add(string field, string fileId)
        {
            _fields.Add(field, fileId);
            _fileIds.Add(fileId, field);
        }

        public void Remove(string field)
        {
            var fileId = _fields[field];
            _fields.Remove(field);
            _fileIds.Remove(fileId);
        }
    }
}