using System;
using System.Collections.Generic;
using System.IO;
using Resin.IO;

namespace Resin
{
    public class FieldWriter : IDisposable
    {
        // terms/docids/term frequency
        private readonly FieldFile _fieldFile;

        // prefix tree
        private readonly Trie _trie;

        private readonly string _termsFileName;
        private readonly string _trieFileName;
        private bool _flushing;

        public FieldWriter(string fileName)
        {
            _termsFileName = fileName;
            _trieFileName = fileName + ".tri";
            _fieldFile = new FieldFile();
            _trie = new Trie();
        }

        public void Write(string docId, string term, int frequency)
        {
            Dictionary<string, int> docs;
            if (!_fieldFile.Terms.TryGetValue(term, out docs))
            {
                docs = new Dictionary<string, int> { { docId, frequency } };
                _fieldFile.Terms.Add(term, docs);
                if(!_fieldFile.DocIds.ContainsKey(docId)) _fieldFile.DocIds.Add(docId, null);
                _trie.Add(term);
            }
            else
            {
                docs[docId] = frequency;
            }
        }

        public void Flush()
        {
            if (_flushing) return;
            _flushing = true;
            _fieldFile.Save(_termsFileName);
            _trie.Save(_trieFileName);
        }

        public void Dispose()
        {
            Flush();
        }
    }
}