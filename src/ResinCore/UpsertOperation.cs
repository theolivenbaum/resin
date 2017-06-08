using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;
using Resin.Sys;
using System.Diagnostics;

namespace Resin
{
    public class UpsertOperation : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UpsertOperation));

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly Compression _compression;
        private readonly long _indexVersionId;
        private readonly DocumentStream _documents;
        private readonly IDocumentStoreWriter _storeWriter;
        private int _count;

        public UpsertOperation(
            string directory, 
            IAnalyzer analyzer, 
            Compression compression, 
            DocumentStream documents, 
            IDocumentStoreWriter storeWriter = null)
        {
            _indexVersionId = Util.GetNextChronologicalFileId();
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;
            _documents = documents;

            var docFileName = Path.Combine(_directory, _indexVersionId + ".rdoc");
            var docAddressFn = Path.Combine(_directory, _indexVersionId + ".da");
            var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pk"));

            _storeWriter = storeWriter ?? new DocumentStoreWriter(
                new DocumentAddressWriter(new FileStream(docAddressFn, FileMode.Create, FileAccess.Write, FileShare.None)), 
                new DocumentWriter(new FileStream(docFileName, FileMode.Create, FileAccess.Write, FileShare.None), _compression), 
                new FileStream(docHashesFileName, FileMode.Create, FileAccess.Write, FileShare.None));
        }

        public long Write()
        {
            var ts = new List<Task>();
            var trieBuilder = new TrieBuilder();
            var posFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pos"));

            var docTimer = new Stopwatch();
            docTimer.Start();

            //Parallel.ForEach(_documents.ReadSource(), doc =>
            foreach (var doc in _documents.ReadSource())
            {
                new SingleDocumentUpsertOperation().Write(
                    doc,
                    _storeWriter,
                    _analyzer,
                    trieBuilder);

                _count++;
            }//);

            Log.InfoFormat("stored {0} documents in {1}", _count, docTimer.Elapsed);

            var posTimer = new Stopwatch();
            posTimer.Start();

            var tries = trieBuilder.GetTries();

            using (var postingsWriter = new PostingsWriter(
                new FileStream(posFileName, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                foreach (var trie in tries)
                {
                    foreach (var node in trie.Value.EndOfWordNodes())
                    {
                        node.PostingsAddress = postingsWriter.Write(node.Postings);
                    }

                    if (Log.IsDebugEnabled)
                    {
                        foreach (var word in trie.Value.Words())
                        {
                            Log.DebugFormat("{0}\t{1}", word.Value, word.Count);
                        }
                    }
                }
            }

            Log.InfoFormat("stored postings refs in trees and wrote postings file in {0}", posTimer.Elapsed);

            var treeTimer = new Stopwatch();
            treeTimer.Start();

            SerializeTries(tries);

            Log.InfoFormat("serialized trees in {0}", treeTimer.Elapsed);

            return _indexVersionId;
        }

        private void SerializeTries(IDictionary<string, LcrsTrie> tries)
        {
            foreach(var t in tries)
            {
                var fileName = Path.Combine(
                    _directory, string.Format("{0}-{1}.tri", _indexVersionId, t.Key));

                t.Value.Serialize(fileName);
            }
        }

        private IEnumerable<Document> ReadSource()
        {
            return _documents.ReadSource();
        }

        public void Dispose()
        {
            _storeWriter.Dispose();

            new IxInfo
            {
                VersionId = _indexVersionId,
                DocumentCount = _count,
                Compression = _compression,
                PrimaryKeyFieldName = _documents.PrimaryKeyFieldName
            }.Serialize(Path.Combine(_directory, _indexVersionId + ".ix"));

        }
    }
}