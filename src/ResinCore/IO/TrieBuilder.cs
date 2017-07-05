using log4net;
using System.Collections.Generic;
using System.Diagnostics;
using DocumentTable;

namespace Resin.IO
{
    public class TrieBuilder
    {
        readonly ILog Log = LogManager.GetLogger(typeof(TrieBuilder));

        private readonly IDictionary<string, LcrsTrie> _tries;
        
        private readonly Stopwatch _timer = new Stopwatch();

        public TrieBuilder()
        {
            _tries = new Dictionary<string, LcrsTrie>();
        }

        public void Add(WordInfo word)
        {
            _timer.Start();

            LcrsTrie trie;

            var key = word.Field.ToHash().ToString();

            if (!_tries.TryGetValue(key, out trie))
            {
                trie = new LcrsTrie();
                _tries.Add(key, trie);
            }

            trie.Add(word.Token, word.Posting);
        }

        public IDictionary<string, LcrsTrie> GetTries()
        {
            Log.InfoFormat("Built in-memory trees in {0}",

            _timer.Elapsed);

            return _tries;
        }
    }
}
