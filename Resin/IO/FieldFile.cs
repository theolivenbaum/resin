using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class FieldFile : FileBase<FieldFile>
    {
        // tokens/docids/term frequency
        private readonly Dictionary<string, Dictionary<string, int>> _tokens;

        private readonly Dictionary<string, object> _docs;

        public FieldFile()  
        {
            _tokens = new Dictionary<string, Dictionary<string, int>>();
            _docs = new Dictionary<string, object>();
        }

        public int NumDocs()
        {
            return _docs.Count;
        }

        public int NumDocs(string token)
        {
            if (!_tokens.ContainsKey(token)) return 0;
            return _tokens[token].Count;
        }

        public bool TryGetValue(string token, out Dictionary<string, int> postings)
        {
            return _tokens.TryGetValue(token, out postings);
        }

        public void AddOrOverwrite(string token, Dictionary<string, int> postings)
        {
            foreach (var posting in postings)
            {
                AddOrOverwrite(posting.Key, token, posting.Value);
            }
        }

        public void AddOrOverwrite(string docId, string token, int frequency)
        {
            Dictionary<string, int> postings;
            if (!TryGetValue(token, out postings))
            {
                postings = new Dictionary<string, int> {{docId, frequency}};
                _tokens[token] = postings;
            }
            else
            {
                postings[docId] = frequency;
            }
            _docs[docId] = null;
        }

        public IEnumerable<KeyValuePair<string, Dictionary<string, int>>> Entries
        {
            get { return _tokens; }
        }

        public void Remove(string docId)
        {
            foreach (var token in _tokens)
            {
                token.Value.Remove(docId);
                if (_tokens[token.Key].Count == 0)
                {
                    _tokens.Remove(token.Key);
                }
            }
            _docs.Remove(docId);
        }
    }
}