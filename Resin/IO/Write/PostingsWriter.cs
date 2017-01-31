using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Resin.IO.Write
{
    public class PostingsWriter : IDisposable
    {
        private readonly StreamWriter _writer;

        public PostingsWriter(StreamWriter writer)
        {
            _writer = writer;
        }

        public void Write(IList<DocumentPosting> postings)
        {
            var serialized = Serialize(postings);

            _writer.WriteLine(serialized);
        }

        private string Serialize(IList<DocumentPosting> postings)
        {
            return JsonConvert.SerializeObject(postings, Formatting.None);
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Dispose();
            }
        }
    }
}