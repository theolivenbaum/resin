using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System;
using Newtonsoft.Json;

namespace Resin
{
    public class JsonStream : DocumentSource, IDisposable
    {
        private readonly StreamReader Reader;
        private readonly int _take;
        private readonly int _skip;

        public JsonStream(string fileName, int skip, int take) 
            : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None), skip, take)
        {
        }

        public JsonStream(Stream stream, int skip, int take)
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

            return ReadInternal().Take(_take);
        }

        private IEnumerable<Document> ReadInternal()
        {
            string line;
            while ((line=Reader.ReadLine()) != "]")
            {
                var dict = JsonConvert.DeserializeObject<IDictionary<string, string>>(line);

                yield return new Document(dict.Select(p=>new Field(p.Key, p.Value)).ToList());
            }
        }

        private IEnumerable<char> ReadUntilTab()
        {
            int c;
            while ((c = Reader.Read()) != -1)
            {
                var ch = (char)c;
                if (ch == '\t') break;
                yield return ch;
            }
        }

        private IEnumerable<Field> Parse(string document, string[] fieldNames)
        {
            var fields = document.Split(new[] { '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            for (int index = 0; index < fields.Length; index++)
            {
                yield return new Field(fieldNames[index], fields[index]);
            }
        }

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}
