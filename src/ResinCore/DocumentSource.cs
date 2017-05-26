using Resin.IO;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System;

namespace Resin
{
    public abstract class DocumentSource
    {
        public abstract IEnumerable<Document> ReadSource();
    }

    public class InMemoryDocumentSource : DocumentSource
    {
        private readonly IEnumerable<Document> _documents;

        public InMemoryDocumentSource(IEnumerable<Document> documents) 
        {
            _documents = documents;
        }
        public override IEnumerable<Document> ReadSource()
        {
            return _documents;
        }
    }

    public class StreamDocumentSource : DocumentSource, IDisposable
    {
        private readonly StreamReader Reader;
        private readonly int _take;
        private readonly int _skip;

        public StreamDocumentSource(string fileName, int skip, int take) 
            : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None), skip, take)
        {
        }

        public StreamDocumentSource(Stream stream, int skip, int take)
        {
            _skip = skip;
            _take = take;

            Reader = new StreamReader(new BufferedStream(stream), Encoding.UTF8);
        }

        private IList<Field> Parse(string document)
        {
            var parts = document.Split(new[] { '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3) return null;

            return new Field[]
            {
                new Field("doctitle", parts[0]),
                new Field("body", parts[2])
            };
        }

        public override IEnumerable<Document> ReadSource()
        {
            Reader.ReadLine(); // first row is junk

            if (_skip > 0)
            {
                int skipped = 0;

                while (skipped++ < _skip)
                {
                    Reader.ReadLine();
                }
            }

            return ReadInternal().Take(_take);
        }

        private IEnumerable<Document> ReadInternal()
        {
            string line;
            while ((line = Reader.ReadLine()) != null)
            {
                var doc = line.Substring(0, line.Length - 1);
                var fields = Parse(doc);
                yield return new Document(fields);
            }
        }

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}
