using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using NetSerializer;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class AnalyzerTests
    {
        [Test]
        public void Test0()
        {
            var serializer = new Serializer(new[] { typeof(string) });
            var fileName = Path.Combine(Setup.Dir, "test.txt");
            //var encoding = Encoding.Default;
            //File.WriteAllText(fileName, "a", encoding);

            using (var fs = File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
            {
                serializer.Serialize(fs, "a");
            }

            using (var mmf = MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, "txt", 0, MemoryMappedFileAccess.ReadWriteExecute))
            using (var mmfs = mmf.CreateViewStream())
            {
                var obj = (string)serializer.Deserialize(mmfs);
                Assert.AreEqual("a", obj);




                //using (var mmf2 = MemoryMappedFile.OpenExisting("txt"))
                using (var mmfs2 = mmf.CreateViewStream())
                {
                    var obj2 = (string)serializer.Deserialize(mmfs2);
                    Assert.AreEqual("a", obj2);

                    //using (var mmf3 = MemoryMappedFile.OpenExisting("txt"))
                    using (var mmfs3 = mmf.CreateViewStream())
                    {
                        serializer.Serialize(mmfs3, "b");
                    }
                }

                //using (var mmf2 = MemoryMappedFile.OpenExisting("txt"))
                using (var mmfs2 = mmf.CreateViewStream())
                {
                    var obj2 = (string)serializer.Deserialize(mmfs2);
                    Assert.AreEqual("b", obj2);
                }

                //using (var mmf2 = MemoryMappedFile.OpenExisting("txt"))
                using (var mmfs2 = mmf.CreateViewStream())
                {
                    serializer.Serialize(mmfs2, "c");
                }

                using (var mmf2 = MemoryMappedFile.OpenExisting("txt"))
                using (var mmfs2 = mmf2.CreateViewStream())
                {
                    var obj2 = (string)serializer.Deserialize(mmfs2);
                    Assert.AreEqual("c", obj2);
                }


                
            }
            
            
        }
        
        [Test]
        public void Stopwords()
        {
            var terms = new Analyzer(stopwords:new[]{"the", "a"}).Analyze("The americans sent us a new movie.").ToList();
            Assert.AreEqual(5, terms.Count);
            Assert.AreEqual("americans", terms[0]);
            Assert.AreEqual("sent", terms[1]);
            Assert.AreEqual("us", terms[2]);
            Assert.AreEqual("new", terms[3]);
            Assert.AreEqual("movie", terms[4]);
        }

        [Test]
        public void Separators()
        {
            var terms = new Analyzer(tokenSeparators:new []{'o'}).Analyze("hello world").ToList();
            Assert.AreEqual(3, terms.Count);
            Assert.AreEqual("hell", terms[0]);
            Assert.AreEqual("w", terms[1]);
            Assert.AreEqual("rld", terms[2]);
        }

        [Test]
        public void Can_analyze()
        {
            var terms = new Analyzer().Analyze("Hello!World?").ToList();
            Assert.AreEqual(2, terms.Count);
            Assert.AreEqual("hello", terms[0]);
            Assert.AreEqual("world", terms[1]);
        }

        [Test, Ignore]
        public void Can_analyze_wierdness()
        {
            var terms = new Analyzer().Analyze("Spanish noblewoman, († 1292) .net c#").ToList();
            Assert.AreEqual(5, terms.Count);
            Assert.AreEqual("spanish", terms[0]);
            Assert.AreEqual("noblewoman", terms[1]);
            Assert.AreEqual("1292", terms[2]);
            Assert.AreEqual(".net", terms[3]);
            Assert.AreEqual("c#", terms[4]);
        }

        [Test]
        public void Can_analyze_common()
        {
            var terms = new Analyzer().Analyze("German politician (CDU)").ToList();
            Assert.AreEqual(3, terms.Count);
            Assert.AreEqual("german", terms[0]);
            Assert.AreEqual("politician", terms[1]);
            Assert.AreEqual("cdu", terms[2]);
        }

        [Test]
        public void Can_analyze_space()
        {
            var terms = new Analyzer().Analyze("   (abc)   ").ToList();
            Assert.AreEqual(1, terms.Count);
            Assert.AreEqual("abc", terms[0]);
        }
    }
}