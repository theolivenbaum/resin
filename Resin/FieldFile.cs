using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class FieldFile : IDisposable
    {
        // tokens/docids/positions
        private readonly IDictionary<string, IDictionary<int, IList<int>>> _tokens;

        private readonly Trie _trie;
        private readonly string _tokenFileName;
        private readonly string _trieFileName;

        public FieldFile(string fileName)
        {
            _tokenFileName = fileName;
            if (File.Exists(fileName))
            {
                using (var file = File.OpenRead(fileName))
                {
                    _tokens = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(file);
                }
            }
            else
            {
                _tokens = new Dictionary<string, IDictionary<int, IList<int>>>();
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

        public void Write(int docId, string token, int position)
        {
            IDictionary<int, IList<int>> docs;
            if (!_tokens.TryGetValue(token, out docs))
            {
                docs = new Dictionary<int, IList<int>> {{docId, new List<int> {position}}};
                _tokens.Add(token, docs);
                _trie.AppendToDescendants(token);
            }
            else
            {
                IList<int> positions;
                if (!docs.TryGetValue(docId, out positions))
                {
                    positions = new List<int>();
                    docs.Add(docId, positions);
                }
                positions.Add(position);
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