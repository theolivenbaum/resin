using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexWriter));
        private readonly IDictionary<string, Trie> _trieFiles;
        private readonly IDictionary<string, PostingsFile> _postingFiles;
        private readonly FixFile _fix;
        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _trieFiles = new Dictionary<string, Trie>();
            _postingFiles = new Dictionary<string, PostingsFile>();
            var fixFileName = Path.Combine(directory, ".fi");
            _fix = File.Exists(fixFileName) ? FixFile.Load(fixFileName) : new FixFile();
        }

        public void Remove(string docId, string field)
        {
            var docFileId = string.Format("{0}.{1}", docId.ToNumericalString(), _fix.Fields[field]);
            var docFileName = Path.Combine(_directory, docFileId + ".do");
            if (!File.Exists(docFileName)) return;
            var file = DocFile.Load(docFileName);
            IEnumerable<string> tokens;
            if (field[0] == '_')
            {
                tokens = new[] {file.Value};
            }
            else
            {
                tokens = _analyzer.Analyze(file.Value);
            }
            foreach (var token in tokens)
            {
                var fileId = string.Format("{0}.{1}", _fix.Fields[field], token.ToNumericalString());
                var fileName = Path.Combine(_directory, fileId + ".po");
                var postingsFile = PostingsFile.Load(fileName);
                postingsFile.Remove(docId);
                if (postingsFile.NumDocs() == 0)
                {
                    Helper.Delete(fileName);
                    var trie = GetTrie(field);
                    trie.Remove(token);
                }
                postingsFile.Save(fileName);
            }
            Helper.Delete(docFileName);
        }

        public void Write(IDictionary<string, string> doc)
        {
            Write(new Document(doc));
        }

        public void Write(Document doc)
        {
            foreach (var field in doc.Fields)
            {
                Analyze(doc.Id, field.Key, field.Value);
                var docFileId = string.Format("{0}.{1}", doc.Id.ToNumericalString(), _fix.Fields[field.Key]);
                var docFileName = Path.Combine(_directory, docFileId + ".do");
                new DocFile(field.Value).Save(docFileName);
            }
        }

        private void Analyze(string docId, string field, string value)
        {
            var termFrequencies = new Dictionary<string, int>();
            var analyze = field[0] != '_';
            if (analyze)
            {
                foreach (var token in _analyzer.Analyze(value))
                {
                    if (termFrequencies.ContainsKey(token)) termFrequencies[token] += 1;
                    else termFrequencies.Add(token, 1);
                }
            }
            else
            {
                if (termFrequencies.ContainsKey(value)) termFrequencies[value] += 1;
                else termFrequencies.Add(value, 1);
            }
            foreach (var token in termFrequencies)
            {
                Write(docId, field, token.Key, token.Value);
            }
        }

        private void Write(string docId, string field, string token, int termFrequency)
        {
            var trie = GetTrie(field);
            trie.Add(token);

            var pf = GetPostingsFile(field, token);
            pf.AddOrOverwrite(docId, termFrequency);
        }

        private PostingsFile GetPostingsFile(string field, string token)
        {
            if (!_fix.Fields.ContainsKey(field))
            {
                _fix.Add(field, Path.GetRandomFileName());
            }
            var fileId = string.Format("{0}.{1}", _fix.Fields[field], token.ToNumericalString());
            PostingsFile file;
            if (!_postingFiles.TryGetValue(fileId, out file))
            {
                var fileName = Path.Combine(_directory, fileId + ".po");
                if (File.Exists(fileName))
                {
                    file = PostingsFile.Load(fileName);
                }
                else
                {
                    file = new PostingsFile();
                }
                _postingFiles.Add(fileId, file);
            }
            return file;
        }

        private Trie GetTrie(string field)
        {
            if (!_fix.Fields.ContainsKey(field))
            {
                _fix.Add(field, Path.GetRandomFileName());
            }
            var fileId = _fix.Fields[field];
            Trie file;
            if (!_trieFiles.TryGetValue(fileId, out file))
            {
                var fileName = Path.Combine(_directory, fileId + ".tr");
                if (File.Exists(fileName))
                {
                    file = Trie.Load(fileName);
                }
                else
                {
                    file = new Trie();
                }
                _trieFiles.Add(fileId, file);
            }
            return file;
        }

        public void Dispose()
        {
            foreach (var trie in _trieFiles)
            {
                try
                {
                    string fileName = Path.Combine(_directory, trie.Key + ".tr");
                    trie.Value.Save(fileName);
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error saving {0} {1}", trie.Key, ex);
                }
            }
            foreach (var pf in _postingFiles)
            {
                try
                {
                    string fileName = Path.Combine(_directory, pf.Key + ".po");
                    pf.Value.Save(fileName);
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error saving {0} {1}", pf.Key, ex);
                }
            }
            _fix.Save(Path.Combine(_directory, ".fi"));
        }
    }
}