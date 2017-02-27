using System;
using System.IO;
using System.Text;

namespace Resin.IO.Read
{
    public class StreamingTrieReader : TrieReader, IDisposable
    {
        private readonly TextReader _textReader;

        public StreamingTrieReader(string fileName)
        {
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None);
            _textReader = new StreamReader(fs, Encoding.Unicode);
        }

        public StreamingTrieReader(TextReader textReader)
        {
            _textReader = textReader;
            LastRead = LcrsNode.MinValue;
            Replay = LcrsNode.MinValue;
        }

        protected override LcrsNode Step()
        {
            if (Replay != LcrsNode.MinValue)
            {
                var replayed = Replay;
                Replay = LcrsNode.MinValue;
                return replayed;
            }

            var data = _textReader.ReadLine();
            
            if (data == null)
            {
                return LcrsNode.MinValue;
            }

            LastRead = new LcrsNode(data);
            return LastRead;
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