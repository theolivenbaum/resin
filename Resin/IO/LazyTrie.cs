using System.Collections.Generic;
using System.IO;

namespace Resin.IO
{
    public class LazyTrie : Trie
    {
        private readonly string _directory;
        private readonly string _field;
        private readonly string _searchPattern;
        private readonly Dictionary<char, Trie> _tries;

        public LazyTrie(string directory, string field)
        {
            _directory = directory;
            _field = field;
            _searchPattern = field.ToTrieSearchPattern();
            _tries = new Dictionary<char, Trie>();
        }

        protected override bool TryResolveChild(char c, out Trie trie)
        {
            var fileName = Path.Combine(_directory, _field.ToTrieFileNameWithoutExtension(c) + ".tr");
            if (File.Exists(fileName))
            {
                if (_tries.ContainsKey(c))
                {
                    trie = _tries[c];
                }
                else
                {
                    trie = Load(fileName);
                    _tries.Add(c, trie);
                }
                return true;
            }
            trie = null;
            return false;
        }

        protected override IEnumerable<Trie> ResolveChildren()
        {
            foreach (var file in Directory.GetFiles(_directory, _searchPattern))
            {
                var c = Path.GetFileNameWithoutExtension(file).ToTrieChar();
                Trie trie;
                if (!_tries.TryGetValue(c, out trie))
                {
                    trie = Load(file);
                    _tries.Add(c, trie);
                }
                yield return trie;
            }
        }
    }
}