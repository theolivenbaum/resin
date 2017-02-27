using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Resin.Analysis;
using Resin.IO.Write;

namespace Resin.IO.Read
{
    public class MappedTrieReader : IDisposable, ITrieReader
    {
        private LcrsNode _lastRead;
        private LcrsNode _replay;
        private readonly int _blockSize;
        private readonly BinaryReader _reader;

        public MappedTrieReader(string fileName)
        {
            _reader = new BinaryReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.Unicode);
            _blockSize = Marshal.SizeOf(typeof(LcrsNode));
            _lastRead = LcrsNode.MinValue;
            _replay = LcrsNode.MinValue;
        }

        private LcrsNode Step()
        {
            if (_replay != LcrsNode.MinValue)
            {
                var replayed = _replay;
                _replay = LcrsNode.MinValue;
                return replayed;
            }

            LcrsNode data;
            try
            {
                var buffer = new byte[_blockSize];
                _reader.Read(buffer, 0, buffer.Length);
                data = LcrsTrieSerializer.BytesToType<LcrsNode>(buffer);
            }
            catch (ArgumentException)
            {
                return LcrsNode.MinValue;
            }

            _lastRead = data;
            return _lastRead;
        }

        private void Replay()
        {
            _replay = _lastRead;
        }

        public bool HasWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("path");

            LcrsNode node;
            if (TryFindDepthFirst(word, 0, out node))
            {
                return node.EndOfWord;
            }
            return false;
        }

        public IEnumerable<Word> StartsWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("path");

            var compressed = new List<Word>();
            LcrsNode node;

            if (TryFindDepthFirst(prefix, 0, out node))
            {
                DepthFirst(prefix, new List<char>(), compressed, prefix.Length-1);
            }

            return compressed;
        }

        public IEnumerable<Word> Near(string word, int edits)
        {
            var compressed = new List<Word>();

            WithinEditDistanceDepthFirst(word, new string(new char[1]), compressed, 0, edits);

            return compressed.OrderBy(w => w.Distance);
        }

        private void WithinEditDistanceDepthFirst(string word, string state, IList<Word> compressed, int depth, int maxEdits)
        {
            var node = Step();

            if (node == LcrsNode.MinValue) return;

            var reachedMin = maxEdits == 0 ? depth >= 0 : depth >= word.Length - 1 - maxEdits;
            var reachedMax = depth >= (word.Length) + maxEdits;
            var nodesWithUnresolvedSiblings = new Stack<Tuple<int, string>>();
            var childIndex = depth + 1;
            string test;

            if (depth == state.Length)
            {
                test = state + node.Value;
            }
            else
            {
                test = new string(state.ReplaceOrAppend(depth, node.Value).Where(c => c != Char.MinValue).ToArray());
            }

            if (reachedMin && !reachedMax)
            {
                var edits = Levenshtein.Distance(word, test);

                if (edits <= maxEdits)
                {
                    if (node.EndOfWord)
                    {
                        compressed.Add(new Word(test) { Distance = edits });
                    }
                }
            }

            if (node.HaveSibling)
            {
                nodesWithUnresolvedSiblings.Push(new Tuple<int, string>(depth, string.Copy(state)));
            }

            // Go left (deep)
            if (node.HaveChild)
            {
                WithinEditDistanceDepthFirst(word, test, compressed, childIndex, maxEdits);
            }

            // Go right (wide)
            foreach (var siblingState in nodesWithUnresolvedSiblings)
            {
                WithinEditDistanceDepthFirst(word, siblingState.Item2, compressed, siblingState.Item1, maxEdits);
            }
        }

        private void DepthFirst(string prefix, IList<char> path, IList<Word> compressed, int depth)
        {
            var node = Step();
            var siblings = new Stack<Tuple<int,IList<char>>>();

            // Go left (deep)
            while (node != LcrsNode.MinValue && node.Depth > depth)
            {
                var copyOfPath = new List<char>(path);

                path.Add(node.Value);

                if (node.EndOfWord)
                {
                    compressed.Add(new Word(prefix + new string(path.ToArray())));
                }

                if (node.HaveSibling)
                {
                    siblings.Push(new Tuple<int, IList<char>>(depth, copyOfPath));
                }

                depth = node.Depth;
                node = Step();
            }

            Replay();

            // Go right (wide)
            foreach (var siblingState in siblings)
            {
                DepthFirst(prefix, siblingState.Item2, compressed, siblingState.Item1);
            }
        }

        private bool TryFindDepthFirst(string path, int currentDepth, out LcrsNode node)
        {
            node = Step();

            while (node != LcrsNode.MinValue && node.Depth != currentDepth)
            {
                node = Step();
            }

            if (node != LcrsNode.MinValue)
            {
                if (node.Value == path[currentDepth])
                {
                    if (currentDepth == path.Length-1)
                    {
                        return true;
                    }
                    // Go left (deep)
                    return TryFindDepthFirst(path, currentDepth+1, out node);
                }
                // Go right (wide)
                return TryFindDepthFirst(path, currentDepth, out node); 
            }

            return false;
        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }
        }
    }
}