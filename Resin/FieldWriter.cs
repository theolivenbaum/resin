using System;
using System.Collections.Generic;
using System.IO;
using Resin.IO;

namespace Resin
{
    public class FieldWriter : IDisposable
    {
        // terms/docids/term frequency
        private readonly FieldFile _terms;

        // prefix tree
        private readonly Trie _trie;

        private readonly string _termsFileName;
        private readonly string _trieFileName;
        private bool _flushed;

        public FieldWriter(string fileName)
        {
            var dir = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _termsFileName = fileName;
            _trieFileName = fileName + ".tri";
            _terms = new FieldFile();
            _trie = new Trie();
        }

        public void Write(string docId, string term, int frequency)
        {
            IDictionary<string, int> docs;
            if (!_terms.Terms.TryGetValue(term, out docs))
            {
                docs = new Dictionary<string, int> { { docId, frequency } };
                _terms.Terms.Add(term, docs);
                _trie.Add(term);
            }
            else
            {
                docs[docId] = frequency;
            }
        }

        public void Flush()
        {
            if (_flushed) return;
            _terms.Save(_termsFileName);
            _trie.Save(_trieFileName);
            _flushed = true;
        }

        public void Dispose()
        {
            Flush();
        }
    }
}