using System.Collections.Generic;
using System.IO;
using Resin.Analysis;
using System.Linq;
using System;
using Resin.IO;
using Resin.Sys;
using log4net;

namespace Resin.Cli
{
    public class CliLineDocUpsertOperation : StreamUpsertOperation
    {
        private readonly int _take;
        private readonly int _skip;
        private int _cursorPosLeft;
        private int _cursorPosTop;
        private readonly bool _autoGeneratePk;
        private readonly string _primaryKey;

        public CliLineDocUpsertOperation(string directory, IAnalyzer analyzer, string fileName, int skip, int take, Compression compression, string primaryKey)
            : base(directory, analyzer, fileName, compression)
        {
            _take = take;
            _skip = skip;
            _cursorPosLeft = Console.CursorLeft;
            _cursorPosTop = Console.CursorTop;
            _autoGeneratePk = string.IsNullOrWhiteSpace(primaryKey);
            _primaryKey = primaryKey;

            Console.WriteLine();
        }

        public CliLineDocUpsertOperation(string directory, IAnalyzer analyzer, Stream file, int skip, int take, Compression compression, string primaryKey)
            : base(directory, analyzer, file, compression)
        {
            _take = take;
            _skip = skip;
            _cursorPosLeft = Console.CursorLeft;
            _cursorPosTop = Console.CursorTop;
            _autoGeneratePk = string.IsNullOrWhiteSpace(primaryKey);
        }

        protected IList<Field> Parse(int id, string document)
        {
            var parts = document.Split(new[] { '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3) return null;

            return new Field[] 
            {
                new Field(id, "doctitle", parts[0]),
                new Field(id, "body", parts[2])
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

        protected virtual IEnumerable<Document> ReadInternal()
        {
            string line;
            var count = 0;

            while ((line = Reader.ReadLine()) != null)
            {
                var doc = line.Substring(0, line.Length - 1);
                var id = count++;
                var fields = Parse(id, doc);

                if (fields != null)
                {
                    string pkVal;

                    if (_autoGeneratePk)
                    {
                        pkVal = Guid.NewGuid().ToString();
                    }
                    else
                    {
                        pkVal = fields.First(f => f.Key == _primaryKey).Value;
                    }

                    var hash = pkVal.ToHash();

                    if (Pks.ContainsKey(hash))
                    {
                        Log.WarnFormat("Found multiple occurrences of documents with pk value of {0} (id:{1}). Only first occurrence will be stored.",
                            pkVal, fields[0].DocumentId);
                    }
                    else
                    {
                        Pks.Add(hash, null);

                        var d = new Document(id, fields);
                        d.Hash = hash;
                        yield return d;

                        var left = Console.CursorLeft;
                        var top = Console.CursorTop;
                        Console.SetCursorPosition(_cursorPosLeft, _cursorPosTop);
                        Console.Write(count);
                        Console.SetCursorPosition(left, top);
                    }
                }
            }
            Console.WriteLine("");
        }
    }
}