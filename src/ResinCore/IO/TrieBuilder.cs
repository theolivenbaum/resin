using DocumentTable;
using log4net;
using Resin.Sys;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Resin.IO
{
    public class TrieBuilder
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(TrieBuilder));
        
        private readonly IDictionary<string, BlockingCollection<IList<WordInfo>>> _queues;
        private readonly IDictionary<string, LcrsTrie> _tries;
        private readonly IList<Task> _consumers;
        private readonly Stopwatch _timer = new Stopwatch();

        public TrieBuilder()
        {
            _queues = new Dictionary<string, BlockingCollection<IList<WordInfo>>>();
            _consumers = new List<Task>();
            _tries = new Dictionary<string, LcrsTrie>();
        }

        public void Add(string fieldName, IList<WordInfo> words)
        {
            _timer.Start();

            BlockingCollection<IList<WordInfo>> queue;

            if (!_queues.TryGetValue(fieldName, out queue))
            {
                queue = new BlockingCollection<IList<WordInfo>>();

                _queues.Add(fieldName, queue);

                var key = fieldName.ToHash().ToString();

                InitTrie(key);

                _consumers.Add(Task.Run(() =>
                {
                    try
                    {
                        Log.InfoFormat("building in-memory tree for field {0}", fieldName);

                        var trie = _tries[key];

                        while (true)
                        {
                            var list = queue.Take();

                            foreach (var word in list)
                            {
                                trie.Add(word.Token, word.Posting);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Done
                    }
                }));
            }

            queue.Add(words);
        }

        private void InitTrie(string key)
        {
            if (!_tries.ContainsKey(key))
            {
                _tries[key] = new LcrsTrie();
            }
        }

        private void CompleteAdding()
        {
            foreach (var queue in _queues.Values)
            {
                queue.CompleteAdding();
            }
        }

        public IDictionary<string, LcrsTrie> GetTries()
        {
            CompleteAdding();

            Task.WaitAll(_consumers.ToArray());

            foreach (var queue in _queues.Values)
            {
                queue.Dispose();
            }

            Log.InfoFormat("Built in-memory trees in {0}", _timer.Elapsed);

            return _tries;
        }
    }
}
