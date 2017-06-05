using Microsoft.VisualStudio.TestTools.UnitTesting;
using Resin.Analysis;
using Resin.IO;
using Resin.Querying;
using Resin.Sys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tests;

namespace Resin.UnitTests
{
    [TestClass]
    public class MergeOperationTests : Setup
    {
        [TestMethod]
        public void Can_merge_two_disk_based_index_versions()
        {
            var dir1 = CreateDir();
            var dir2 = CreateDir();
            var dir3 = CreateDir();

            var docs1 = new List<dynamic>
            {
                new {_id = "0", title = "rambo first blood" },
                new {_id = "1", title = "the raid" },
                new {_id = "2", title = "rocky 2" },
            }.ToDocuments();

            var docs2 = new List<dynamic>
            {
                new {_id = "3", title = "rambo 2" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments();

            long index1 = new UpsertOperation(
                dir1, 
                new Analyzer(), 
                compression: Compression.NoCompression, 
                primaryKeyFieldName: 
                "_id", 
                documents: docs1).Write();

            long index2 = new UpsertOperation(
                dir2,
                new Analyzer(),
                compression: Compression.NoCompression,
                primaryKeyFieldName:
                "_id",
                documents: docs2).Write();

            var ix1 = Path.Combine(dir1, index1 + ".ix");
            var ix2 = Path.Combine(dir2, index2 + ".ix");

            var index3 = new MergeOperation().Merge(
                ix1,
                ix2,
                dir3,
                Compression.NoCompression,
                "_id");

            using (var collector = new Collector(dir3, IxInfo.Load(Path.Combine(dir3, index3 + ".ix")), new Tfidf()))
            {
                var scores = collector.Collect(new QueryContext("title", "rambo")).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
            }
        }
    }
}
