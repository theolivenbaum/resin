using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class IndexWriterTests
    {
        [Test]
        public void Can_write_one_field()
        {
            const string dir = Setup.Dir + "\\Can_write_one_field";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Hello World!"}.ToList()}
                    }
                });
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Goodbye Cruel World."}.ToList()}
                    }
                });
            }

            Assert.AreEqual(1, Directory.GetFiles(dir, "0.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "0.ix.dix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld.tri").Length);
        }

        [Test]
        public void Can_write_two_fields()
        {
            const string dir = Setup.Dir + "\\Can_write_two_fields";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Hello World!"}.ToList()},
                        {"body", new []{"Once upon a time there was a man and a woman."}.ToList()}
                    }
                });
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "0.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "0.ix.dix").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld.tri").Length);
        }

        

        //[Test]
        //public void Can_append_to_one_field()
        //{
        //    const string dir = "c:\\temp\\resin_tests\\Can_append_one_field";
        //    if(Directory.Exists(dir)) Directory.Delete(dir, true);

        //    using (var w = new IndexWriter(dir, new Analyzer()))
        //    {
        //        w.Write(new Document
        //        {
        //            Id = 0,
        //            Fields = new Dictionary<string, List<string>>
        //            {
        //                {"title", new []{"Hello World!"}.ToList()},
        //            }
        //        });
        //    }

        //    Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);

        //    var parser = new QueryParser(new Analyzer());
        //    using (var reader = new IndexReader(new Scanner(dir)))
        //    {
        //        var terms = reader.Scanner.GetAllTokens("title").Select(t => t.Token).ToList();

        //        Assert.AreEqual(2, terms.Count);
        //        Assert.IsTrue(terms.Contains("hello"));
        //        Assert.IsTrue(terms.Contains("world"));

        //        var docs = reader.GetScoredResult(parser.Parse("title:world")).ToList();

        //        Assert.AreEqual(1, docs.Count);

        //        docs = reader.GetScoredResult(parser.Parse("title:cruel")).ToList();

        //        Assert.AreEqual(0, docs.Count);
        //    }

        //    using (var w = new IndexWriter(dir, new Analyzer()))
        //    {
        //        w.Write(new Document
        //        {
        //            Id = 1,
        //            Fields = new Dictionary<string, List<string>>
        //            {
        //                {"title", new []{"Hello Cruel World!"}.ToList()},
        //            }
        //        });
        //    }

        //    Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);

        //    using (var reader = new IndexReader(new Scanner(dir)))
        //    {
        //        var terms = reader.Scanner.GetAllTokens("title").Select(t => t.Token).ToList();

        //        Assert.AreEqual(3, terms.Count);
        //        Assert.IsTrue(terms.Contains("hello"));
        //        Assert.IsTrue(terms.Contains("world"));
        //        Assert.IsTrue(terms.Contains("cruel"));

        //        var docs = reader.GetScoredResult(parser.Parse("title:world")).ToList();

        //        Assert.AreEqual(2, docs.Count);

        //        docs = reader.GetScoredResult(parser.Parse("title:cruel")).ToList();

        //        Assert.AreEqual(1, docs.Count);
        //    }
        //}

        //[Test]
        //public void Can_append_to_two_fields()
        //{
        //    const string dir = "c:\\temp\\resin_tests\\Can_append_two_fields";
        //    using (var w = new IndexWriter(dir, new Analyzer()))
        //    {
        //        w.Write(new Document
        //        {
        //            Id = 0,
        //            Fields = new Dictionary<string, List<string>>
        //            {
        //                {"title", new []{"Hello World!"}.ToList()},
        //                {"body", new []{"Once upon a time there was a man and a woman."}.ToList()}
        //            }
        //        });
        //    }
        //    Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
        //    using (var w = new IndexWriter(dir, new Analyzer()))
        //    {
        //        w.Write(new Document
        //        {
        //            Id = 0,
        //            Fields = new Dictionary<string, List<string>>
        //            {
        //                {"title", new []{"Goodbye Cruel World."}.ToList()},
        //                {"body", new []{"Once upon a time there was a cat and a dog."}.ToList()}
        //            }
        //        });
        //    }
        //    Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
        //}
    }
}