using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Markup;

namespace Resin.IO
{
    public class TrieReader : IDisposable
    {
        private readonly StreamReader _reader;
        private Line? _buffer;
        private int _level;
        public TrieReader(StreamReader reader)
        {
            _reader = reader;
            _buffer = Read();
        }

        //public IEnumerable<string> Similar(string word, int edits)
        //{

        //}

        //public IEnumerable<string> Prefixed(string word)
        //{

        //}


        public bool HasWord(string word)
        {
            foreach (var c in word)
            {
                var line = Scan(c);
                if (!line.HasValue) return false;
                GoToNextLevel();
            }
            return true;
        }

        private Line? Step()
        {
            if (_buffer == null) return null;

            var line = new Line(_buffer.Value.Level, _buffer.Value.Value, _buffer.Value.EoW);
            _buffer = Read();
            if (line.Level > _level)
            {
                _level++;
                return null;
            }
            return line;
        }

        private Line? Scan(char value)
        {
            while (true)
            {
                var line = Step();
                if (!line.HasValue) return null;
                if (line.Value.Value == value)
                {
                    return line;
                }
            }
        }

        private void GoToNextLevel()
        {
            while (true)
            {
                var line = Step();
                if (line == null) break;
            }

        }

        private Line? Read()
        {
            var line = _reader.ReadLine();
            if (string.IsNullOrEmpty(line)) return null;
            var segs = line.Split(new[]{"\t"}, StringSplitOptions.None);
            var level = int.Parse(segs[0]);
            var val = Char.Parse(segs[1]);
            var eow = segs[2] == "1";
            return new Line(level, val, eow);
        }

        private struct Line
        {
            public readonly int Level; 
            public readonly bool EoW;
            public readonly char Value;

            public Line(int level, char value, bool eow)
            {
                Level = level;
                EoW = eow;
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString(CultureInfo.CurrentCulture);
            }
        }


        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Close();
                _reader.Dispose();
            }
        }
    }

    public interface ITrieReader
    {
        IEnumerable<string> Similar(string word, int edits);
        IEnumerable<string> Prefixed(string prefix);
        bool HasWord(string word);
    }
}