using System;
using System.Collections.Generic;
using Resin.Analysis;

namespace Resin
{
    public class WriteSession : IDisposable
    {
        private readonly string _directory;
        private readonly IndexBuilder _builder;

        public WriteSession(string directory, IAnalyzer analyzer, IEnumerable<Document> documents)
        {
            _directory = directory;
            _builder = new IndexBuilder(analyzer, documents);
        }

        public string Write()
        {
            var index = _builder.ToIndex();
            index.Serialize(_directory);
            return index.Info.Name;
        }

        public void Dispose()
        {
        }
    }
}