using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Resin.Analysis;

namespace Resin.IO.Read
{
    public class StreamTreeScanner : IDisposable
    {
        private readonly TextReader _textReader;
        private LcrsNode _lastRead;
        private LcrsNode _replay;

        public StreamTreeScanner(string fileName)
        {
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None);
            _textReader = new StreamReader(fs, Encoding.Unicode);
        }

        public StreamTreeScanner(TextReader textReader)
        {
            _textReader = textReader;
        }

        private LcrsNode Step(TextReader sr)
        {
            if (_replay != null)
            {
                var replayed = _replay;
                _replay = null;
                return replayed;
            }
            var data = sr.ReadLine();
            
            if (data == null)
            {
                return null;
            }
            _lastRead = new LcrsNode(data);
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
            return Near(word, edits, word.Length);
        }

        public IEnumerable<Word> Near(string word, int edits, int minLength)
        {
            var words = new List<Word>();

            WithinEditDistanceDepthFirst(word, new string(new char[word.Length]), 0, edits, minLength, words);
            
            return words.OrderBy(w => w.Distance);
        }

        private void WithinEditDistanceDepthFirst(string word, string state, int depth, int maxEdits, int minLength, IList<Word> words)
        {
            var node = Step(_textReader);

            var nodesWithUnresolvedSiblings = new Stack<Tuple<int, string>>();
            var childIndex = depth + 1;

            // Go left (deep)
            if (node != null)
            {
                string test;
                if (depth == state.Length)
                {
                    test = state + node.Value;
                }
                else
                {
                    test = new string(state.ReplaceOrAppend(depth, node.Value).Where(c => c != Char.MinValue).ToArray());
                }

                if (test.Length >= minLength)
                {
                    var edits = Levenshtein.Distance(word, test);

                    if (edits <= maxEdits && node.EndOfWord)
                    {
                        words.Add(new Word(test){Distance = edits });
                    } 
                }

                if (node.HaveSibling)
                {
                    nodesWithUnresolvedSiblings.Push(new Tuple<int, string>(depth, string.Copy(state)));
                }

                if (node.HaveChild)
                {
                    WithinEditDistanceDepthFirst(word, string.Copy(test), childIndex, maxEdits, minLength, words);
                }

                // Go right (wide)
                foreach (var siblingState in nodesWithUnresolvedSiblings)
                {
                    WithinEditDistanceDepthFirst(word, siblingState.Item2, siblingState.Item1, maxEdits, minLength, words);
                }
            }
        }

        private void DepthFirst(string prefix, IList<char> path, IList<Word> compressed, int depth)
        {
            var node = Step(_textReader);
            var siblings = new Stack<Tuple<int,IList<char>>>();

            // Go left (deep)
            while (node != null && node.Depth > depth)
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
                node = Step(_textReader);
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
            node = Step(_textReader);

            while (node != null && node.Depth != currentDepth)
            {
                node = Step(_textReader);
            }

            if (node != null)
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
            if (_textReader != null)
            {
                _textReader.Dispose();
            }
        }
    }
}