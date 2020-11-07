using Microsoft.Extensions.Logging;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    public class IndexFileStreamProvider : IDisposable
    {
        private readonly ulong _collectionId;
        private readonly SessionFactory _sessionFactory;
        private readonly ILogger _logger;

        public IndexFileStreamProvider(ulong collectionId, SessionFactory sessionFactory, ILogger logger = null)
        {
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _logger = logger??sessionFactory.Logger;
        }

        public void Dispose()
        {
        }

        public void Write(IDictionary<long, VectorNode> index)
        {
            using (var postingsStream = _sessionFactory.CreateAppendStream(_collectionId, "pos"))
            using (var vectorStream = _sessionFactory.CreateAppendStream(_collectionId, "vec"))
            {
                foreach (var column in index)
                {
                    using (var indexStream = _sessionFactory.CreateAppendStream(_collectionId, column.Key, "ix"))
                    using (var columnWriter = new ColumnStreamWriter(indexStream))
                    using (var pageIndexWriter = new PageIndexWriter(_sessionFactory.CreateAppendStream(_collectionId, column.Key, "ixtp")))
                    {
                        var size = columnWriter.CreatePage(column.Value, vectorStream, postingsStream, pageIndexWriter);

                        if (_logger != null)
                            _logger.LogInformation($"serialized column {column.Key}, weight {column.Value.Weight} {size}");
                    }
                }
            }
        }

        public void WriteOneHotVectors(IDictionary<long, VectorNode> index)
        {
            foreach (var column in index)
            {
                var matrix = PathFinder.AsOneHotMatrix(column.Value);

                using (var vectorStream = _sessionFactory.CreateAppendStream(_collectionId, column.Key, "1h.vec"))
                {
                    foreach (var row in matrix)
                    {
                        var vector = new IndexedVector(row);
                        vector.Serialize(vectorStream);
                    }
                }
            }
        }
    }
}