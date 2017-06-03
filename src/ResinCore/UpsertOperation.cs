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
    public class UpsertOperation
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UpsertOperation));

        private readonly Dictionary<ulong, object> _primaryKeys;
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly Compression _compression;
        private readonly long _indexVersionId;
        private readonly bool _autoGeneratePk;
        private readonly string _primaryKey;
        private readonly DocumentSource _documents;

        public UpsertOperation(
            string directory, IAnalyzer analyzer, Compression compression, string primaryKey, DocumentSource documents)
        {
            _indexVersionId = Util.GetNextChronologicalFileId();
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;
            _autoGeneratePk = string.IsNullOrWhiteSpace(primaryKey);
            _primaryKey = primaryKey;
            _primaryKeys = new Dictionary<UInt64, object>();
            _documents = documents;
        }

        public long Write()
        {
            var ts = new List<Task>();
            var trieBuilder = new TrieBuilder();
            var count = 0;
            var docFileName = Path.Combine(_directory, _indexVersionId + ".rdoc");
            var docAddressFn = Path.Combine(_directory, _indexVersionId + ".da");
            var posFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pos"));
            var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pk"));

            var docTimer = new Stopwatch();
            docTimer.Start();

            using (var docAddressWriter = new DocumentAddressWriter(new FileStream(docAddressFn, FileMode.Create, FileAccess.Write, FileShare.None)))
            using (var docWriter = new DocumentWriter(new FileStream(docFileName, FileMode.Create, FileAccess.Write, FileShare.None), _compression))
            using (var docHashesStream = new FileStream(docHashesFileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (var doc in ReadSourceAndAssignIdentifiers())
                {
                    new SingleDocumentUpsertOperation().Write(
                        doc, 
                        docAddressWriter, 
                        docWriter, 
                        docHashesStream, 
                        _analyzer,
                        trieBuilder);

                    count++;
                }

                trieBuilder.CompleteAdding();
            }

            Log.InfoFormat("stored {0} documents in {1}", count, docTimer.Elapsed);

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
                            Log.Debug(word);
                        }
                    }
                }
            }

            Log.InfoFormat("stored postings refs in trees and wrote postings file in {0}", posTimer.Elapsed);

            var treeTimer = new Stopwatch();
            treeTimer.Start();

            SerializeTries(tries);

            Log.InfoFormat("serialized trees in {0}", treeTimer.Elapsed);

            new IxInfo
            {
                VersionId = _indexVersionId,
                DocumentCount = count,
                Compression = _compression
            }.Serialize(Path.Combine(_directory, _indexVersionId + ".ix"));

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

        private IEnumerable<Document> ReadSourceAndAssignIdentifiers()
        {
            var count = 0;
            foreach (var document in ReadSource())
            {
                string pkVal;

                if (_autoGeneratePk)
                {
                    pkVal = Guid.NewGuid().ToString();
                }
                else
                {
                    pkVal = document.Fields[_primaryKey].Value;
                }

                var hash = pkVal.ToHash();

                if (_primaryKeys.ContainsKey(hash))
                {
                    Log.WarnFormat("Found multiple occurrences of documents with pk value of {0} (id:{1}). First occurrence will be stored.",
                        pkVal, document.Id);
                }
                else
                {
                    _primaryKeys.Add(hash, null);

                    document.Hash = hash;
                    document.Id = count++;

                    yield return document;
                }
            }
        }
    }
}