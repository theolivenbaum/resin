using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.VectorSpace
{
    /// <summary>
    /// Read (reduce) postings.
    /// </summary>
    public class PostingsReader
    {
        private readonly IStreamFactory _sessionFactory;
        private readonly IDictionary<(ulong collectionId, long keyId), Stream> _streams;
        private readonly string _directory;

        public PostingsReader(string directory, IStreamFactory sessionFactory)
        {
            _directory = directory;
            _sessionFactory = sessionFactory;
            _streams = new Dictionary<(ulong collectionId, long keyId), Stream>();
        }

        public IList<(ulong, long)> Read(ulong collectionId, long keyId, IList<long> offsets)
        {
            var time = Stopwatch.StartNew();
            var list = new List<(ulong, long)>();

            foreach (var postingsOffset in offsets)
                GetPostingsFromStream(collectionId, keyId, postingsOffset, list);

            _sessionFactory.LogDebug($"read {list.Count} postings from {offsets.Count} terms into memory in {time.Elapsed}");

            return list;
        }

        private void GetPostingsFromStream(ulong collectionId, long keyId, long postingsOffset, IList<(ulong collectionId, long docId)> result)
        {
            var stream = GetOrCreateStream(collectionId, keyId);

            stream.Seek(postingsOffset, SeekOrigin.Begin);

            Span<byte> buf = stackalloc byte[sizeof(long)];

            stream.Read(buf);

            var numOfPostings = BitConverter.ToInt64(buf);

            var len = sizeof(long) * numOfPostings;

            Span<byte> listBuf = new byte[len];

            var read = stream.Read(listBuf);

            if (read != len)
                throw new DataMisalignedException();

            foreach (var docId in MemoryMarshal.Cast<byte, long>(listBuf))
            {
                result.Add((collectionId, docId));
            }
        }

        private Stream GetOrCreateStream(ulong collectionId, long keyId)
        {
            Stream stream;
            var key = (collectionId, keyId);

            if (!_streams.TryGetValue(key, out stream))
            {
                stream = _sessionFactory.CreateReadStream(Path.Combine(_directory, $"{collectionId}.{keyId}.pos"));
                _streams.Add(key, stream);
            }

            return stream;
        }

        public void Dispose()
        {
            foreach (var stream in _streams.Values)
                stream.Dispose();
        }
    }
}
