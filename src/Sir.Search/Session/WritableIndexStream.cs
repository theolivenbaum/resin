using Microsoft.Extensions.Logging;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sir.Search
{
    public class WritableIndexStream : IDisposable
    {
        private readonly string _directory;
        private readonly ulong _collectionId;
        private readonly Database _sessionFactory;
        private readonly ILogger _logger;
        private readonly IDictionary<(long keyId, string fileExtension), Stream> _streams;

        public WritableIndexStream(
            string directory,
            ulong collectionId, 
            Database sessionFactory, 
            ILogger logger = null)
        {
            _directory = directory;
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _logger = logger;
            _streams = new Dictionary<(long, string), Stream>();
        }

        public void Dispose()
        {
            foreach (var stream in _streams.Values)
            {
                stream.Dispose();
            }
        }

        public void Write(IDictionary<long, VectorNode> index)
        {
            foreach (var column in index)
            {
                var time = Stopwatch.StartNew();
                var vectorStream = GetOrCreateAppendStream(column.Key, "vec");
                var postingsStream = GetOrCreateAppendStream(column.Key, "pos");

                using (var columnWriter = new ColumnWriter(GetOrCreateAppendStream(column.Key, "ix"), keepStreamOpen: true))
                using (var pageIndexWriter = new PageIndexWriter(GetOrCreateAppendStream(column.Key, "ixtp"), keepStreamOpen: true))
                {
                    var size = columnWriter.CreatePage(column.Value, vectorStream, postingsStream, pageIndexWriter);

                    if (_logger != null)
                        _logger.LogInformation($"serialized column {column.Key}, weight {column.Value.Weight} {size} in {time.Elapsed}");
                }
            }
        }

        private Stream GetOrCreateAppendStream(long keyId, string fileExtension)
        {
            Stream stream;
            var key = (keyId, fileExtension);

            if (!_streams.TryGetValue(key, out stream))
            {
               stream = _sessionFactory.CreateAppendStream(_directory, _collectionId, keyId, fileExtension);
                _streams.Add(key, stream);
            }

            return stream;
        }
    }
}