using Microsoft.Extensions.Logging;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Search
{
    public class IndexMemoryStreamProvider : IDisposable
    {
        private readonly ILogger _logger;

        public IndexMemoryStreamProvider(ILogger logger = null)
        {
            _logger = logger;
        }

        public void Dispose()
        {
        }

        public void Write(IDictionary<long, VectorNode> index)
        {
            using (var postingsStream = new MemoryStream())
            using (var vectorStream = new MemoryStream())
            {
                foreach (var column in index)
                {
                    using (var indexStream = new MemoryStream())
                    using (var columnWriter = new ColumnStreamWriter(indexStream))
                    using (var pageIndexWriter = new PageIndexWriter(new MemoryStream()))
                    {
                        var size = columnWriter.CreatePage(column.Value, vectorStream, postingsStream, pageIndexWriter);

                        if (_logger != null)
                            _logger.LogInformation($"serialized column {column.Key}, weight {column.Value.Weight} {size}");
                    }
                }
            }
        }
    }
}