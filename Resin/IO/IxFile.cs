using System;
using System.Collections.Generic;

namespace Resin.IO
{
    /// <summary>
    /// Index header file representing either an index or a commit. Contains pointers to docs and fields.
    /// </summary>
    [Serializable]
    public class IxFile : FileBase<IxFile>
    {
        private readonly string _fixFileName;
        private readonly string _dixFileName;
        private readonly List<string> _deletedDocs;

        public IxFile( string dixFileName, List<string> deletedDocs)
        {
            _dixFileName = dixFileName;
            _deletedDocs = deletedDocs;
        }

        public IxFile(string fixFileName, string dixFileName, List<string> deletedDocs) : this(dixFileName, deletedDocs)
        {
            _fixFileName = fixFileName;
        }

        public string FixFileName { get { return _fixFileName; } }
        public string DixFileName { get { return _dixFileName; } }
        public IList<string> DeletedDocs { get { return _deletedDocs; } }
    }
}