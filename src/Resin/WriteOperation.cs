using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.Analysis;
using Resin.IO;
using Resin.Sys;

namespace Resin
{
    public class WriteOperation : IDisposable
    {
        private readonly string _directory;
        private readonly IndexBuilder _builder;

        public WriteOperation(string directory, IAnalyzer analyzer, IEnumerable<Document> documents)
        {
            _directory = directory;
            _builder = new IndexBuilder(analyzer, documents);
        }

        public string Execute()
        {
            var index = _builder.ToIndex();
            index.Serialize(_directory);
            return index.Info.Name;
        }

        public void Dispose()
        {
        }
    }

    public class DeleteOperation : IDisposable
    {
        private readonly string _directory;
        private readonly IEnumerable<string> _documentIds;
        private readonly string _indexName;

        public DeleteOperation(string directory, IEnumerable<string> documentIds)
        {
            _directory = directory;
            _documentIds = documentIds;
            _indexName = Util.GetChronologicalFileId();
        }

        public void Execute()
        {
            var ix = new IxInfo
            {
                Name = _indexName,
                DocumentCount = new DocumentCount(new Dictionary<string, int>()),
                Deletions = _documentIds.ToList()
            };
            ix.Save(Path.Combine(_directory, ix.Name + ".ix"));
        }

        public void Dispose()
        {
        }
    }
}