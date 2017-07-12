using log4net;
using System.Collections.Generic;
using System.Diagnostics;
using DocumentTable;

namespace Resin.IO
{
    public class TrieBuilder
    {
        readonly ILog Log = LogManager.GetLogger(typeof(TrieBuilder));

        private readonly IDictionary<ulong, LcrsTrie> _tries;
        
        private readonly Stopwatch _timer = new Stopwatch();

        public TrieBuilder()
        {
            _tries = new Dictionary<ulong, LcrsTrie>();
        }

        public void Add(string key, string value, DocumentPosting posting)
        {
            _timer.Start();

            LcrsTrie trie;

            var hashedKey = key.ToHash();

            if (!_tries.TryGetValue(hashedKey, out trie))
            {
                trie = new LcrsTrie();
                _tries.Add(hashedKey, trie);
            }

            trie.Add(value, 0, posting);
        }

        public IDictionary<ulong, LcrsTrie> GetTries()
        {
            Log.InfoFormat("Built in-memory trees in {0}",

            _timer.Elapsed);

            return _tries;
        }
    }
}
