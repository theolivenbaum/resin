using System.Collections.Generic;

namespace Resin.IO
{
    public class TrieScanner : Trie
    {
        private readonly int _count;
        private bool _resolved;
        private readonly TrieStreamReader _trieStreamReader;

        public TrieScanner(TrieStreamReader trieStreamReader)
        {
            _trieStreamReader = trieStreamReader;

            var root = _trieStreamReader.Read();
            _count = root.Count;
        }

        public TrieScanner(char value, bool eow, int count, TrieStreamReader trieStreamReader)
        {
            Value = value;
            Eow = eow;
            _count = count;
            _trieStreamReader = trieStreamReader;
        }

        protected override int GetCount()
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
                _trieStreamReader.ResolveChildren(this);
                _resolved = true;
            }
        }

        protected override bool TryResolveChild(char c, out Trie child)
        {
            Resolve();
            return base.TryResolveChild(c, out child);
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