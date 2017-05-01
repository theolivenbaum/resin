using System.Linq;
using NUnit.Framework;
using Resin.IO;
using System.IO;
using Resin.IO.Read;

namespace Tests
{
    [TestFixture]
    public class LcrsNodeTests
    {
        public void Can_serialize_trie()
        {
            var one = new LcrsTrie('\0', false);
            one.Add("ape");
            one.Add("app");
            one.Add("banana");

            var bytes = new LcrsNode(one, 0).Serialize();

            using (var reader = new MappedTrieReader(new MemoryStream(bytes)))
            {
                Word found;
                Assert.IsTrue(reader.HasWord("ape", out found));
            }
            using (var reader = new MappedTrieReader(new MemoryStream(bytes)))
            {
                Word found;
                Assert.IsTrue(reader.HasWord("app", out found));
            }
            using (var reader = new MappedTrieReader(new MemoryStream(bytes)))
            {
                Word found;
                Assert.IsTrue(reader.HasWord("banana", out found));
            }
        }
        
    }
}