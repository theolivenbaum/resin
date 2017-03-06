using System;
using System.Collections.Generic;
using System.IO;
using Resin.Sys;

namespace Resin.IO.Read
{
    public class PostingsReader : IDisposable
    {
        private readonly Stream _stream;

        public PostingsReader(Stream stream)
        {
            _stream = stream;
        }

        public IEnumerable<DocumentPosting> Read(Term term)
        {
            var termHash = (term.Field + term.Word.Value).ToHash();

            foreach (var posting in (List<DocumentPosting>) BinaryFile.Serializer.Deserialize(_stream))
            {
                if (posting.Term == termHash)
                {
                    posting.Field = term.Field;
                    yield return posting;
                }
            }
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