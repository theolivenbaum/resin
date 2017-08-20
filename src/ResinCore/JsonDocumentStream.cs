using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System;
using Newtonsoft.Json;
using Resin.Documents;

namespace Resin
{
    public class JsonDocumentStream : DocumentStream, IDisposable
    {
        private readonly StreamReader Reader;
        private readonly int _take;
        private readonly int _skip;

        public JsonDocumentStream(string fileName, int skip, int take, string primaryKeyFieldName = null) 
            : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), skip, take, primaryKeyFieldName)
        {
        }

        public JsonDocumentStream(Stream stream, int skip, int take, string primaryKeyFieldName = null)
            : base(primaryKeyFieldName)
        {
            _skip = skip;
            _take = take;

            Reader = new StreamReader(new BufferedStream(stream), Encoding.UTF8);
        }

        public override IEnumerable<Document> ReadSource()
        {
            Reader.ReadLine();

            if (_skip > 0)
            {
                int skipped = 0;

                while (skipped++ < _skip)
                {
                    Reader.ReadLine();
                }
            }

            return ReadSourceAndAssignPk( ReadInternal().Take(_take));
        }

        private IEnumerable<Document> ReadInternal()
        {
            string line;
            while ((line=Reader.ReadLine()) != "]")
            {
                if (line == null) break;

                var dict = JsonConvert.DeserializeObject<IDictionary<string, string>>(line);

                yield return new Document(dict.Select(p=>new Field(p.Key, p.Value)).ToList());
            }
        }

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}