using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class FieldFile : FileBase<FieldFile>
    {
        // tokens/docids/term frequency
        private readonly Dictionary<string, Dictionary<string, int>> _tokens;

        public Dictionary<string, object> DocIds { get; set; }

        public FieldFile()  
        {
            _tokens = new Dictionary<string, Dictionary<string, int>>();
            DocIds = new Dictionary<string, object>();
        }

        public Dictionary<string, Dictionary<string, int>> Tokens
        {
            get { return _tokens; }
        }

        public void Remove(string docId)
        {
            if (!DocIds.ContainsKey(docId)) return;

            foreach (var token in _tokens)
            {
                token.Value.Remove(docId);
            }
            DocIds.Remove(docId);
        }
    }
}