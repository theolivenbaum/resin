using log4net;
using Resin.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Resin.Documents;

namespace Resin
{
    public class FullTextWriteSession : WriteSession
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FullTextWriteSession));

        private readonly TreeBuilder _treeBuilder;
        private readonly PostingsWriter _postingsWriter;

        public FullTextWriteSession(string directory, Compression compression, TreeBuilder treeBuilder)
            :base(directory, compression)
        {
            _treeBuilder = treeBuilder;

            _postingsWriter = new PostingsWriter(
                new FileStream(
                    Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName() + ".pos"),
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    4096,
                    FileOptions.DeleteOnClose
                    ));
        }

        protected override void DoFlush(Stream dataFile)
        {
            var posTimer = Stopwatch.StartNew();

            var tries = _treeBuilder.GetTrees();

            foreach (var trie in tries)
            {
                var nodes = trie.Value.EndOfWordNodes();

                foreach (var node in nodes)
                {
                    node.PostingsAddress = _postingsWriter.Write(node.PostingsStream);
                }

                //if (Log.IsDebugEnabled)
                //{
                //    foreach (var word in trie.Value.Words())
                //    {
                //        Log.Debug(word.Value);
                //    }
                //}
            }

            Log.InfoFormat(
                "stored postings in in {0}",
                posTimer.Elapsed);

            var treeTimer = Stopwatch.StartNew();

            Version.FieldOffsets = SerializeTries(tries, dataFile);

            Log.InfoFormat("serialized trees in {0}", treeTimer.Elapsed);

            var postingsTimer = Stopwatch.StartNew();

            Version.PostingsOffset = dataFile.Position;
            _postingsWriter.Stream.Flush();
            _postingsWriter.Stream.Position = 0;
            _postingsWriter.Stream.CopyTo(dataFile);

            Log.InfoFormat("copied postings to data file in {0}", postingsTimer.Elapsed);

            base.DoFlush(dataFile);
        }

        private IDictionary<ulong, long> SerializeTries(
            IDictionary<ulong, LcrsTrie> tries, Stream stream)
        {
            var offsets = new Dictionary<ulong, long>();

            foreach (var t in tries)
            {
                offsets.Add(t.Key, stream.Position);

                t.Value.Serialize(stream);
            }

            return offsets;
        }

        protected override void SaveSegmentInfo(SegmentInfo ix)
        {
            Serializer.Serialize(
                (FullTextSegmentInfo)ix, Path.Combine(_directory, ix.Version + ".ix"));
        }

        protected override SegmentInfo CreateNewSegmentInfo(long version)
        {
            return new FullTextSegmentInfo { Version = version };
        }
    }


}
