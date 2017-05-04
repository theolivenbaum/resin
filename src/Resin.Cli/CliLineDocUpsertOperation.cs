using System.Collections.Generic;
using System.IO;
using Resin.Analysis;
using System.Linq;
using System;
using Resin.IO;

namespace Resin.Cli
{
    public class CliLineDocUpsertOperation : StreamUpsertOperation
    {
        private readonly int _take;
        private readonly int _skip;
        private int _cursorPos;

        public CliLineDocUpsertOperation(string directory, IAnalyzer analyzer, string fileName, int skip, int take, bool compression, string primaryKey)
            : base(directory, analyzer, fileName, compression, primaryKey)
        {
            _take = take;
            _skip = skip;
            _cursorPos = Console.CursorLeft;
        }

        public CliLineDocUpsertOperation(string directory, IAnalyzer analyzer, Stream file, int skip, int take, bool compression, string primaryKey)
            : base(directory, analyzer, file, compression, primaryKey)
        {
            _take = take;
            _skip = skip;
            _cursorPos = Console.CursorLeft;
        }

        protected IList<Field> Parse(string document)
        {
            var parts = document.Split(new[] { '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3) return null;

            return new Field[] 
            {
                new Field("doctitle", parts[0]),
                new Field("body", parts[2])
            };
        }

        protected override IEnumerable<Document> ReadSource()
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
            var took = 0;
            string line;

            while ((line = Reader.ReadLine()) != null)
            {
                var doc = line.Substring(0, line.Length - 1);
                var dic = Parse(doc);

                if (dic != null)
                {
                    yield return new Document(dic);

                    Console.SetCursorPosition(_cursorPos, Console.CursorTop);
                    Console.Write(++took);
                }
            }
            Console.WriteLine("");
        }
    }
}