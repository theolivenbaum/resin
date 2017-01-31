using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Resin.IO.Read
{
    public class PostingsReader : IDisposable
    {
        private readonly StreamReader _sr;

        public PostingsReader(StreamReader sr)
        {
            _sr = sr;
        }

        private void Reset()
        {
            _sr.BaseStream.Position = 0;
            _sr.BaseStream.Seek(0, SeekOrigin.Begin);
            _sr.DiscardBufferedData();
        }

        public IList<DocumentPosting> Read(int rowIndex)
        {
            Reset();

            var timer = new Stopwatch();
            timer.Start();

            var row = 0;

            while (row++ < rowIndex)
            {
                _sr.ReadLine();
            }

            var postings = JsonConvert.DeserializeObject<IList<DocumentPosting>>(_sr.ReadLine());

            return postings;
        }

        public void Dispose()
        {
            if (_sr != null)
            {
                _sr.Dispose();
            }
        }
    }
}