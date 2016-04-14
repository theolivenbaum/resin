using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class FieldFile : FileBase<FieldFile>
    {
        // terms/docids/term frequency
        private readonly Dictionary<string, Dictionary<string, int>> _terms;

        public FieldFile()
        {
            _terms = new Dictionary<string, Dictionary<string, int>>();
        }

        public Dictionary<string, Dictionary<string, int>> Terms
        {
            get { return _terms; }
        }
    }
}