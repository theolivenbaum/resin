using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Resin.IO
{
    public class LazyTrie : Trie
    {
        private readonly string _directory;
        private readonly string _field;
        private readonly string _searchPattern;

        public LazyTrie(string directory, string field)
        {
            _directory = directory;
            _field = field;
            _searchPattern = field.CreateTrieFileSearchPattern();
        }

        protected override bool TryResolveChild(char c, out Trie trie)
        {
            if (Nodes.TryGetValue(c, out trie))
            {
                return true;
            }
            var fileName = Path.Combine(_directory, _field.ToTrieFileNameWoExt(c) + ".tr");
            if (File.Exists(fileName))
            {
                trie = Load(fileName);
                if (trie == null) throw new DataMisalignedException("Your data got misaligned.");
                Nodes[c] = trie;
                return true;
            }
            return false;
        }

        public override IEnumerable<Trie> ResolveChildren()
        {
            foreach (var file in Directory.GetFiles(_directory, _searchPattern))
            {
                var c = Path.GetFileNameWithoutExtension(file).ParseCharFromTrieFileName();
                Trie trie;
                if (TryResolveChild(c, out trie))
                {
                    yield return trie;
                }
            }
            foreach (var node in Nodes.Values)
            {
                yield return node;
            }
        }

        public override IEnumerable<Trie> Dirty()
        {
            return Nodes.Values;
        }
    }
}