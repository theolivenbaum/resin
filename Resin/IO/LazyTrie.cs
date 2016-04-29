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
            _searchPattern = field.ToTrieSearchPattern();
        }

        protected override bool TryResolveChild(char c, out Trie trie)
        {
            var fileName = Path.Combine(_directory, _field.ToTrieFileNameWithoutExtension(c) + ".tr");
            if (File.Exists(fileName))
            {
                trie = Load(fileName);
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
                if (!_children.TryGetValue(c, out trie))
                {
                    trie = Load(file);
                    _children.Add(c, trie);
                }
                yield return trie;
            }
        }
    }
}