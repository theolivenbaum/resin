using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class FieldWriter : IDisposable
    {
        // tokens/docids/term frequency
        private readonly IDictionary<string, IDictionary<int, int>> _tokens;

        private readonly Trie _trie;
        private readonly string _tokenFileName;
        private readonly string _trieFileName;

        public FieldWriter(string fileName)
        {
            _tokenFileName = fileName;
            if (File.Exists(fileName))
            {
                using (var file = File.OpenRead(fileName))
                {
                    _tokens = Serializer.Deserialize<Dictionary<string, IDictionary<int, int>>>(file);
                }
            }
            else
            {
                _tokens = new Dictionary<string, IDictionary<int, int>>();
            }
            _trieFileName = fileName + ".tri";
            if (File.Exists(_trieFileName))
            {
                _trie = Trie.Load(_trieFileName);
            }
            else
            {
                _trie = new Trie();
            }
        }

        public void Write(int docId, string token, int frequency)
        {
            IDictionary<int, int> docs;
            if (!_tokens.TryGetValue(token, out docs))
            {
                docs = new Dictionary<int, int> { { docId, frequency } };
                _tokens.Add(token, docs);
                _trie.AddWord(token);
            }
            else
            {
                docs[docId] = frequency;
            }
        }

        private void Flush()
        {
            var dir = Path.GetDirectoryName(_tokenFileName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(_tokenFileName))
            {
                Serializer.Serialize(fs, _tokens);
            }
            _trie.Save(_trieFileName);
        }

        public void Dispose()
        {
            Flush();
        }
    }
}