using System;
using Resin.IO;

namespace Resin
{
    public class FieldWriter : IDisposable
    {
        // terms/docids/term frequency
        private readonly FieldFile _fieldFile;

        // prefix tree
        private readonly Trie _trie;
        private readonly string _fieldFileName;
        private readonly string _trieFileName;
        private bool _flushing;

        public FieldWriter(string fileName)
        {
            _fieldFileName = fileName;
            _trieFileName = fileName + ".tri";
            _fieldFile = new FieldFile();
            _trie = new Trie();
        }

        public void Write(string docId, string token, int frequency, bool analyzed)
        {
            _fieldFile.AddOrOverwrite(docId, token, frequency);
            if (analyzed)
            {
                _trie.Add(token);
            }
        }

        public void Flush()
        {
            if (_flushing) return;
            _flushing = true;
            _fieldFile.Save(_fieldFileName);
            _trie.Save(_trieFileName);
        }

        public void Dispose()
        {
            Flush();
        }
    }
}