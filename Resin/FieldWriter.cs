using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class FieldWriter : IDisposable
    {
        // terms/docids/term frequency
        private IDictionary<string, IDictionary<int, int>> _terms;

        // prefix tree
        private Trie _trie;

        private readonly string _termsFileName;
        private readonly string _trieFileName;

        public FieldWriter(string fileName)
        {
            var dir = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _termsFileName = fileName;
            _trieFileName = fileName + ".tri";
            _terms = new Dictionary<string, IDictionary<int, int>>();
            _trie = new Trie();
        }

        public void Write(int docId, string term, int frequency)
        {
            IDictionary<int, int> docs;
            if (!_terms.TryGetValue(term, out docs))
            {
                docs = new Dictionary<int, int> { { docId, frequency } };
                _terms.Add(term, docs);
                _trie.Add(term);
            }
            else
            {
                docs[docId] = frequency;
            }
        }

        private void Flush()
        {
            using (var fs = File.Create(_termsFileName))
            {
                Serializer.Serialize(fs, _terms);
            }
            _trie.Save(_trieFileName);
        }

        public void Dispose()
        {
            Flush();
        }
    }
}