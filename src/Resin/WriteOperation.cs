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

        public DeleteOperation(string directory, IEnumerable<string> documentIds)
        {
            _directory = directory;
            _documentIds = documentIds;
        }

        public void Execute()
        {
            var fileId = Util.GetChronologicalFileId();
            new DelInfo {DocIds = _documentIds.ToList()}.Save(Path.Combine(_directory, fileId + ".del"));
        }

        public void Dispose()
        {
        }
    }
}