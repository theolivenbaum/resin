using System.Collections.Generic;
using DocumentTable;

namespace Resin.IO
{
    public class TreeBuilder
    {
        private readonly IDictionary<ulong, LcrsTrie> _tries;
        
        public TreeBuilder()
        {
            _tries = new Dictionary<ulong, LcrsTrie>();
        }

        public void Add(string key, string value, IList<DocumentPosting> postings)
        {
            var tree = GetTree(key);

            tree.Add(value, 0, postings);
        }

        private LcrsTrie GetTree(string key)
        {
            LcrsTrie trie;
            var hashedKey = key.ToHash();

            if (!_tries.TryGetValue(hashedKey, out trie))
            {
                trie = new LcrsTrie();
                _tries.Add(hashedKey, trie);
            }

            return trie;
        }

        public IDictionary<ulong, LcrsTrie> GetTrees()
        {
            return _tries;
        }
    }
}
