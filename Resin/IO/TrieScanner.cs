using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    public class TrieScanner : Trie
    {
        private readonly int _count;
        private bool _resolved;
        private readonly TrieStreamReader _trieStreamReader;
        public static int Skip;

        public TrieScanner(TrieStreamReader trieStreamReader)
        {
            _trieStreamReader = trieStreamReader;

            var root = _trieStreamReader.Reset();
            Value = root.Value;
            _count = root.ChildCount;
        }

        public TrieScanner(char value, bool eow, int count, TrieStreamReader trieStreamReader)
        {
            Value = value;
            Eow = eow;
            _count = count;
            _trieStreamReader = trieStreamReader;
        }

        protected override int GetChildCount()
        {
            return _count;
        }

        protected override IEnumerable<Trie> GetChildren()
        {
            Resolve();
            return base.GetChildren();
        }

        private void Resolve()
        {
            if (!_resolved)
            {
                if (Skip > 0)
                {
                    _trieStreamReader.Skip(Skip);
                    Skip = 0;
                }
                _trieStreamReader.ResolveChildren(this);
                _resolved = true;
            }
        }

        protected override bool TryResolveChild(char c, out Trie child)
        {
            Resolve();
            if (base.TryResolveChild(c, out child))
            {
                var nephews = NodeDict.Values.Take(child.Index).Sum(x => x.ChildCount);
                Skip += nephews;
                return true;
            }
            return false;
        }

        public override void SimScan(string word, string state, int edits, int index, IList<Word> words)
        {
            var childIndex = index + 1;
            var children = GetChildren().ToList();
            foreach (var child in children)
            {
                var tmp = index == state.Length ? state + child.Value : state.ReplaceAt(index, child.Value);
                if (child.Eow)
                {
                    var potential = tmp.Substring(0, childIndex);
                    var distance = Levenshtein.Distance(word, potential);
                    if (distance <= edits)
                    {
                        words.Add(new Word { Value = potential, Distance = distance });
                    }
                }
                child.SimScan(word, tmp, edits, childIndex, words);
            }
        }

        public override void Dispose()
        {
            if (_trieStreamReader != null)
            {
                _trieStreamReader.Dispose();
            }
        }
    }
}