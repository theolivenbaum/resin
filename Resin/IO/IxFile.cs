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
        private readonly List<Term> _deletions;

        public IxFile(string fixFileName, string dixFileName, List<Term> deletions)
        {
            _fixFileName = fixFileName;
            _dixFileName = dixFileName;
            _deletions = deletions;
        }

        public string FixFileName { get { return _fixFileName; } }
        public string DixFileName { get { return _dixFileName; } }
        public IList<Term> Deletions { get { return _deletions; } }
    }
}