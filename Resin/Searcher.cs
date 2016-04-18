using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class Searcher : SearcherBase, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly QueryParser _parser;

        public Searcher(string directory, QueryParser parser) : base(directory, new Dictionary<string, DocFile>(), new Dictionary<string, FieldFile>(), new Dictionary<string, Trie>(), new Dictionary<string, Document>())
        {
            _parser = parser;

            var generations = Helper.GetIndexFiles(Directory).ToList();
            var ix = IxFile.Load(generations.First());
            Dix = DixFile.Load(Path.Combine(Directory, ix.DixFileName));
            Fix = FixFile.Load(Path.Combine(Directory, ix.FixFileName));
            var optimizer = new Optimizer(directory, generations.ToArray(), Dix, Fix, DocFiles, FieldFiles, TrieFiles, Docs);
            optimizer.Rebase();
            Log.DebugFormat("searcher initialized in {0}", Directory);
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(Directory, Fix, FieldFiles, TrieFiles);
            var scored = collector.Collect(_parser.Parse(query), page, size).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }

        public void Dispose()
        {
        }
    }
}