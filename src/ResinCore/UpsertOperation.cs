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
using System.Linq;

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
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;
            _documents = documents;

            var lastVersion = Directory.GetFiles(directory, "*.ix").OrderBy(fn => fn)
                .LastOrDefault();

            if (lastVersion == null)
            {
                _indexVersionId = Util.GetNextChronologicalFileId();
            }
            else
            {
                if (WriteLockExists() || !TryAquireWriteLock())
                {
                    _indexVersionId = Util.GetNextChronologicalFileId();
                }
                else
                {
                    _indexVersionId = long.Parse(Path.GetFileNameWithoutExtension(lastVersion));

                    var ix = IxInfo.Load(lastVersion);

                    _count = ix.DocumentCount;
                }
            }

            _storeWriter = storeWriter ??
                new DocumentStoreWriter(directory, _indexVersionId, _compression);
        }

        private bool TryAquireWriteLock()
        {
            var tmp = Path.Combine(_directory, "write._lock");
            var lockFile = Path.Combine(_directory, "write.lock");
            
            File.Create(Path.Combine(_directory, tmp));
            try
            {
                File.Copy(tmp, lockFile);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void ReleaseFileLock()
        {
            File.Delete(Path.Combine(_directory, "write.lock"));
        }

        private bool WriteLockExists()
        {
            return File.Exists(Path.Combine(_directory, "write.lock"));
        }

        public long Write()
        {
            var ts = new List<Task>();
            var trieBuilder = new TrieBuilder();
            var posFileName = Path.Combine(
                _directory, string.Format("{0}.{1}", _indexVersionId, "pos"));

            var docTimer = Stopwatch.StartNew();

            foreach (var doc in _documents.ReadSource())
            {
                doc.Id = _count++;

                new SingleDocumentUpsertOperation().Write(
                    doc,
                    _storeWriter,
                    _analyzer,
                    trieBuilder);
            }

            Log.InfoFormat("stored {0} documents in {1}", _count+1, docTimer.Elapsed);

            var posTimer = Stopwatch.StartNew();

            var tries = trieBuilder.GetTries();

            using (var postingsWriter = new PostingsWriter(
                new FileStream(posFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
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

            Log.InfoFormat(
                "stored postings refs in trees and wrote postings file in {0}", 
                posTimer.Elapsed);

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

            var tmpFileName = Path.Combine(_directory, _indexVersionId + "._ix");
            var fileName = Path.Combine(_directory, _indexVersionId + ".ix");
            
            new IxInfo
            {
                VersionId = _indexVersionId,
                DocumentCount = _count,
                Compression = _compression,
                PrimaryKeyFieldName = _documents.PrimaryKeyFieldName
            }.Serialize(tmpFileName);

            File.Copy(tmpFileName, fileName, overwrite:true);
            File.Delete(tmpFileName);

            ReleaseFileLock();
        }
    }
}