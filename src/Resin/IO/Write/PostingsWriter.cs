using System;
using System.Collections.Generic;
using System.IO;
using Resin.Sys;

namespace Resin.IO.Write
{
    public class PostingsWriter : IDisposable
    {
        private readonly Stream _stream;

        public PostingsWriter(Stream stream)
        {
            _stream = stream;
        }

        public void Write(Dictionary<Term, List<DocumentPosting>> postings)
        {
            var tagged = new List<DocumentPosting>();

            foreach (var term in postings)
            {
                var termHash = (term.Key.Field+term.Key.Word.Value).ToHash();

                foreach (var posting in term.Value)
                {
                    posting.Term = termHash;
                    tagged.Add(posting);
                }
            }

            BinaryFile.Serializer.Serialize(_stream, tagged);
        }

        public void Dispose()
        {
            if (_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
            }
        }
    }
}