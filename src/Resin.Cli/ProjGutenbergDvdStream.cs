using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using DocumentTable;
using System.IO.Compression;

namespace Resin
{
    public class ProjGutenbergDvdStream : DocumentStream
    {
        private readonly int _take;
        private readonly int _skip;
        private readonly string _directory;

        public ProjGutenbergDvdStream(string directory, int skip, int take)
            : base("uri")
        {
            _directory = directory;
            _skip = skip;
            _take = take;
        }

        public override IEnumerable<Document> ReadSource()
        {
            return ReadSourceAndAssignPk(ReadInternal().Skip(_skip).Take(_take));
        }

        private IEnumerable<Document> ReadInternal()
        {
            var files = Directory.GetFiles(_directory, "*.zip", SearchOption.AllDirectories);
            foreach (var zipFileName in files)
            {
                if (zipFileName.StartsWith("\\ETEXT"))
                {
                    continue;
                }

                using (var file = new FileStream(zipFileName, FileMode.Open))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                using (var txt = zip.Entries[0].Open())
                using(var reader = new StreamReader(txt))
                {
                    var title = reader.ReadLine() + " " + reader.ReadLine();
                    var head = new StringBuilder();
                    var couldNotRead = false;
                    string encoding = null;

                    while (true)
                    {
                        var line = reader.ReadLine();

                        if (line == null)
                        {
                            couldNotRead = true;
                            break;
                        }
                        else if (line.Contains("***"))
                        {
                            break;
                        }

                        if (line.Contains("encoding: ASCII"))
                        {
                            encoding = line;
                        }
                        else
                        {
                            head.Append(" ");
                            head.Append(line);
                        }
                        
                    }

                    if (encoding == null || couldNotRead)
                    {
                        continue;
                    }

                    var body = reader.ReadToEnd();

                    var document = new Document(
                        new List<Field>
                        {
                            new Field("title", title
                                .Replace("The Project Gutenberg EBook of ", "")
                                .Replace("Project Gutenberg's", "")
                                .Replace("The Project Gutenberg eBook, ", "")),
                            new Field("head", head),
                            new Field("body", body),
                            new Field("uri", zipFileName.Replace(_directory, ""))
                        });

                    yield return document;
                }
            }
        }
    }
}