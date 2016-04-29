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

        private readonly ConcurrentDictionary<char, Trie> _cache;

        public LazyTrie(string directory, string field)
        {
            _directory = directory;
            _field = field;
            _searchPattern = field.ToTrieSearchPattern();
            _cache = new ConcurrentDictionary<char, Trie>();
        }

        protected override bool TryResolveChild(char c, out Trie trie)
        {
            if (_cache.TryGetValue(c, out trie))
            {
                return true;
            }
            var fileName = Path.Combine(_directory, _field.ToTrieFileNameWithoutExtension(c) + ".tr");
            if (File.Exists(fileName))
            {
                trie = Load(fileName);
                if (trie == null) throw new DataMisalignedException("Your data got misaligned.");
                _cache[c] = trie;
                return true;
            }
            return _children.TryGetValue(c, out trie);
        }

        protected override IEnumerable<Trie> ResolveChildren()
        {
            foreach (var file in Directory.GetFiles(_directory, _searchPattern))
            {
                var c = Path.GetFileNameWithoutExtension(file).ToTrieChar();
                Trie trie;
                if (TryResolveChild(c, out trie))
                {
                    yield return trie;
                }
            }
        }
    }
}