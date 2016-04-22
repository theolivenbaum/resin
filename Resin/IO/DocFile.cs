using System;

namespace Resin.IO
{
    /// <summary>
    /// </summary>
    [Serializable]
    public class DocFile : FileBase<DocFile>
    {
        // the value (verbatim) of a document field
        private readonly string _value;
        public string Value { get { return _value; } }
        public DocFile(string value)
        {
            _value = value;
        }
    }
}