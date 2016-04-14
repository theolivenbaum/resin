using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class FieldFile : FileBase<FieldFile>
    {
        // terms/docids/term frequency
        private readonly Dictionary<string, Dictionary<string, int>> _terms;

        public Dictionary<string, object> DocIds { get; set; }
 
        public FieldFile()
        {
            _terms = new Dictionary<string, Dictionary<string, int>>();
            DocIds = new Dictionary<string, object>();
        }

        public Dictionary<string, Dictionary<string, int>> Terms
        {
            get { return _terms; }
        }
    }
}