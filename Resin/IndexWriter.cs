using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly Analyzer _analyzer;
        private readonly IDictionary<string, int> _fieldIndex; 
        private readonly IDictionary<int, FieldFile> _fieldFiles;
        private readonly string _fieldIndexFileName;
        private readonly IList<DocumentInfo> _docQueue1;
        private readonly bool _overwrite;

        public IndexWriter(string directory, Analyzer analyzer, bool overwrite = true)
        {
            _directory = directory;
            _analyzer = analyzer;
            _overwrite = overwrite;

            _fieldFiles = new Dictionary<int, FieldFile>();
            _fieldIndexFileName = Path.Combine(_directory, "fld.ix");
            _docQueue1 = new List<DocumentInfo>();

            if (!overwrite && File.Exists(_fieldIndexFileName))
            {
                using (var fs = File.OpenRead(_fieldIndexFileName))
                {
                    _fieldIndex = Serializer.Deserialize<Dictionary<string, int>>(fs);
                }
            }
            else
            {
                _fieldIndex = new Dictionary<string, int>();
            }

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }

        public void Write(Document doc)
        {
            // Prepare the message
            var docInfo = new DocumentInfo
            {
                Id = doc.Id,
                Fields = doc.Fields.ToDictionary(x => x.Key, y => new FieldInfo {Values = y.Value})
            };

            foreach (var field in docInfo.Fields)
            {
                // Make sure there is a file for each field

                // First find the field id
                int fieldId;
                if (!_fieldIndex.TryGetValue(field.Key, out fieldId))
                {
                    fieldId = GetNextFreeFieldId();
                    _fieldIndex.Add(field.Key, fieldId);
                }

                // Enrich the message with the field id 
                field.Value.FieldId = fieldId;

                FieldFile ff;
                if (!_fieldFiles.TryGetValue(fieldId, out ff))
                {
                    var fileName = Path.Combine(_directory, fieldId + ".fld");
                    ff = new FieldFile(fileName, _overwrite);
                    _fieldFiles.Add(fieldId, ff);
                }
            }

            // Hand over the message to the writer threads
            _docQueue1.Add(docInfo);

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
            //var batches = _docQueue1.GroupBy(d=>d.Id).ToList().IntoBatches(2000).ToList();
            //var fields = new ConcurrentBag<FieldFileEntry>();
            var fields = new List<FieldFileEntry>();
            //Parallel.ForEach(batches, batch =>
            //foreach (var batch in batches)
            //{
                using (var docFile = new MultiDocumentFile(_directory))
                {
                    foreach (var doc in _docQueue1.GroupBy(d => d.Id).ToList())
                    {
                        foreach (var di in doc)
                        {
                            foreach (var field in di.Fields)
                            {
                                foreach (var value in field.Value.Values)
                                {
                                    docFile.Write(di.Id, field.Key, value);
                                    fields.Add(new FieldFileEntry {DocId = di.Id, FieldId = field.Value.FieldId, Value = value});
                                }
                            }
                        }
                    }
                }
            //}
            var groupedFields = fields.GroupBy(f => f.FieldId).ToList();
            Parallel.ForEach(groupedFields, field =>
            {
                using (var fieldFile = _fieldFiles[field.Key])
                {
                    foreach (var f in field)
                    {
                        var terms = _analyzer.Analyze(f.Value);
                        for (int position = 0; position < terms.Length; position++)
                        {
                            fieldFile.Write(f.DocId, terms[position], position);
                        }
                    }
                }
            });
            using (var fs = File.Create(_fieldIndexFileName))
            {
                Serializer.Serialize(fs, _fieldIndex);
            }
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