using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class ScannerTests
    {
        [Test]
        public void Can_score()
        {
            const string dir = Setup.Dir + "\\Can_score";
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(new Document
                {
                    Fields = new Dictionary<string, string>
                        {
                            {"title", "hello hello"},
                            {"id", "0"}
                        }
                });

                writer.Write(new Document
                {
                    Fields = new Dictionary<string, string>
                        {
                            {"title", "hello hello hello"},
                            {"id", "1"}
                        }
                });

                writer.Write(new Document
                {
                    Fields = new Dictionary<string, string>
                        {
                            {"title", "hello"},
                            {"id", "2"}
                        }
                });

                writer.Write(new Document
                {
                    Fields = new Dictionary<string, string>
                        {
                            {"title", "rambo"},
                            {"id", "3"}
                        }
                });
            }
            var scanner = FieldScanner.MergeLoad(dir);
            var score = scanner.GetDocIds(new Term {Field = "title", Token = "hello", And = true}).OrderByDescending(d=>d.TermFrequency).ToList();

            Assert.AreEqual(3, score.Count);

            Assert.AreEqual("1", score[0].DocId);
            Assert.AreEqual("0", score[1].DocId);
            Assert.AreEqual("2", score[2].DocId);
        }


        [Test]
        public void Can_scan()
        {
            const string dir = Setup.Dir + "\\Can_scan";
            const string text = "we all live in a yellow submarine";
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(new Document
                {
                    Fields = new Dictionary<string, string>
                        {
                            {"title", text},
                            {"id", "0"}
                        }
                });
            }
            var scanner = FieldScanner.MergeLoad(dir);
            Assert.AreEqual(1, scanner.GetDocIds(new Term {Field = "title", Token = "we", And = true}).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "all", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "live", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "in", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "a", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "yellow", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "submarine", And = true }).ToList().Count);
        }

        [Test]
        public void Can_scan_document_with_multiple_values()
        {
            const string dir = Setup.Dir + "\\Can_scan_document_with_multiple_values";
            const string text = "we all live in a yellow submarine";
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(new Document
                {
                    Fields = new Dictionary<string, string>
                        {
                            {"title", text},
                            {"id", "0"}
                        }
                });
            }
            var scanner = FieldScanner.MergeLoad(dir);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "we", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "all", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "live", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "in", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "a", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "yellow", And = true }).ToList().Count);
            Assert.AreEqual(1, scanner.GetDocIds(new Term { Field = "title", Token = "submarine", And = true }).ToList().Count);
        }
    }
}