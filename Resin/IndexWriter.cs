using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ProtoBuf;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly Analyzer _analyzer;
        private readonly bool _overwrite;
        private readonly IDictionary<string, int> _fieldIndex; 
        private readonly IDictionary<int, FieldFile> _fieldFiles;
        private readonly IDictionary<int, DocumentFile> _docFiles;
        private readonly TaskQueue<DocumentInfo> _docQueue;
        private readonly TaskQueue<FieldFileEntry> _fieldQueue;

        public IndexWriter(string directory, Analyzer analyzer, bool overwrite = true)
        {
            _directory = directory;
            _analyzer = analyzer;
            _overwrite = overwrite;
            _fieldIndex = new Dictionary<string, int>();
            _fieldFiles = new Dictionary<int, FieldFile>();
            _docFiles = new Dictionary<int, DocumentFile>();
            _docQueue = new TaskQueue<DocumentInfo>(1, WriteToDocFile);
            _fieldQueue = new TaskQueue<FieldFileEntry>(1, WriteToFieldFile);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }

        public void Write(Document doc)
        {
            // Make sure there is a document file
            DocumentFile df;
            if (!_docFiles.TryGetValue(doc.Id, out df))
            {
                var fileName = Path.Combine(_directory, doc.Id + ".d");
                df = new DocumentFile(fileName);
                _docFiles.Add(doc.Id, df);
            }

            var docInfo = new DocumentInfo
            {
                Id = doc.Id,
                Fields = doc.Fields.ToDictionary(x => x.Key, y => new FieldInfo {Values = y.Value})
            };


            foreach (var field in docInfo.Fields)
            {
                // Make sure there is a file for each field
                int fieldId;
                if (!_fieldIndex.TryGetValue(field.Key, out fieldId))
                {
                    fieldId = GetNextFreeFieldId();
                    _fieldIndex.Add(field.Key, fieldId);
                }
                field.Value.FieldId = fieldId;

                FieldFile ff;
                if (!_fieldFiles.TryGetValue(fieldId, out ff))
                {
                    var fileName = Path.Combine(_directory, fieldId + ".fld");
                    ff = new FieldFile(fileName);
                    _fieldFiles.Add(fieldId, ff);
                }
            }

            _docQueue.Enqueue(docInfo);

        }

        private void WriteToDocFile(DocumentInfo doc)
        {
            var docFile = _docFiles[doc.Id];
            foreach (var field in doc.Fields)
            {
                foreach (var value in field.Value.Values)
                {
                    docFile.Write(field.Key, value);
                    _fieldQueue.Enqueue(new FieldFileEntry{DocId = doc.Id, FieldId = field.Value.FieldId, Value = value});
                }
            }
            //TODO: FLUSH?
        }

        private void WriteToFieldFile(FieldFileEntry field)
        {
            var fieldFile = _fieldFiles[field.FieldId];
            var terms = _analyzer.Analyze(field.Value);
            for(int position = 0; position < terms.Length; position++)
            {
                fieldFile.Write(field.DocId, terms[position], position);
            }
            //TODO: FLUSH?
        }

        private int GetNextFreeFieldId()
        {
            //var liveFieldFiles = Directory.GetFiles(_directory, "*.fld");
            //if (liveFieldFiles.Length > 0)
            //{
            //    var highestLiveFieldId = liveFieldFiles
            //    .Select(f=>int.Parse(Path.GetFileNameWithoutExtension(f)))
            //    .OrderBy(i=>i)
            //    .Last();
            //    return highestLiveFieldId + _fieldIndex.Count + 1;
            //}
            return _fieldIndex.Count;
        }

        private void Flush()
        {
            _docQueue.Dispose();
            _fieldQueue.Dispose();

            foreach (var w in _fieldFiles.Values)
            {
                w.Dispose();
            }
            foreach (var w in _docFiles.Values)
            {
                w.Dispose();
            }

            var indexFileName = Path.Combine(_directory, "field.idx");
            IDictionary<string, IList<int>> fieldIndex;
            if (_overwrite)
            {
                fieldIndex = new Dictionary<string, IList<int>>();
                foreach (var entry in _fieldIndex)
                {
                    fieldIndex.Add(entry.Key, new List<int> { entry.Value });
                }
            }
            else
            {
                if (File.Exists(indexFileName))
                {
                    using (var fs = File.OpenRead(indexFileName))
                    {
                        fieldIndex = Serializer.Deserialize<Dictionary<string, IList<int>>>(fs);
                    }
                    foreach (var entry in _fieldIndex)
                    {
                        IList<int> fieldIds;
                        if (fieldIndex.TryGetValue(entry.Key, out fieldIds))
                        {
                            fieldIds.Add(entry.Value);
                        }
                        else
                        {
                            fieldIds = new List<int> { entry.Value };
                            fieldIndex.Add(entry.Key, fieldIds);
                        }
                    }
                }
                else
                {
                    fieldIndex = new Dictionary<string, IList<int>>();
                    foreach (var entry in _fieldIndex)
                    {
                        fieldIndex.Add(entry.Key, new List<int> { entry.Value });
                    }
                }
            }

            var tmpFile = Path.Combine(_directory, "field.idx.tmp");
            using (var fs = File.Create(tmpFile))
            {
                Serializer.Serialize(fs, fieldIndex);
            }
            if (File.Exists(indexFileName)) File.Delete(indexFileName);
            File.Move(tmpFile, indexFileName);
            //var indexedFields = fieldIndex.Values.SelectMany(l => l).ToList();
            //foreach (var file in Directory.GetFiles(_directory, "*.fld"))
            //{
            //    var fieldId = int.Parse(Path.GetFileNameWithoutExtension(file));
            //    if (!indexedFields.Contains(fieldId)) File.Delete(file);
            //}
        }

        public void Dispose()
        {
            Flush();
        }
    }

    public class TaskQueue<T> : IDisposable where T : class
    {
        private readonly Action<T> _action;
        readonly object _sync = new object();
        readonly Thread[] _workers;
        readonly Queue<T> _tasks = new Queue<T>();

        public TaskQueue(int workerCount, Action<T> action)
        {
            _action = action;
            _workers = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                (_workers[i] = new Thread(Consume)).Start();
                Trace.WriteLine("worker thread started");
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _workers.Length; i++) Enqueue(null);
            foreach (Thread worker in _workers)
            {
                worker.Join();
                Trace.WriteLine("worker thread joined");
            }
        }

        public void Enqueue(T task)
        {
            lock (_sync)
            {
                _tasks.Enqueue(task);
                Monitor.PulseAll(_sync);
            }
        }

        void Consume()
        {
            while (true)
            {
                T task;
                lock (_sync)
                {
                    while (_tasks.Count == 0) Monitor.Wait(_sync);
                    task = _tasks.Dequeue();
                }
                if (task == null) return; //exit
                _action(task);
            }
        }

        public int Count { get { return _tasks.Count; } }
    }
}