using Microsoft.Extensions.Logging;
using Sir.VectorSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Sir.Search
{
    public class WritableIndexStream : IDisposable
    {
        private readonly ulong _collectionId;
        private readonly SessionFactory _sessionFactory;
        private readonly ILogger _logger;
        private readonly Stream _postingsStream;
        private readonly Stream _vectorStream;
        private readonly ConcurrentDictionary<(long keyId, string fileExtension), Stream> _streams;

        public WritableIndexStream(
            ulong collectionId, 
            SessionFactory sessionFactory, 
            ILogger logger = null)
        {
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _logger = logger;
            _postingsStream = _sessionFactory.CreateAppendStream(_collectionId, "pos");
            _vectorStream = _sessionFactory.CreateAppendStream(_collectionId, "vec");
            _streams = new ConcurrentDictionary<(long, string), Stream>();
        }

        public void Dispose()
        {
            foreach (var stream in _streams.Values)
            {
                stream.Dispose();
            }

            _postingsStream.Dispose();
            _vectorStream.Dispose();
        }

        public void Write(IDictionary<long, VectorNode> index)
        {
            foreach (var column in index)
            {
                using (var columnWriter = new ColumnWriter(GetOrCreateAppendStream(column.Key, "ix"), keepStreamOpen:true))
                using (var pageIndexWriter = new PageIndexWriter(GetOrCreateAppendStream(column.Key, "ixtp"), keepStreamOpen:true))
                {
                    var size = columnWriter.CreatePage(column.Value, _vectorStream, _postingsStream, pageIndexWriter);

                    if (_logger != null)
                        _logger.LogInformation($"serialized column {column.Key}, weight {column.Value.Weight} {size}");
                }
            }
        }

        private Stream GetOrCreateAppendStream(long keyId, string fileExtension)
        {
            return _streams.GetOrAdd(
                (keyId, fileExtension), 
                _sessionFactory.CreateAppendStream(_collectionId, keyId, fileExtension));
        }
    }
}