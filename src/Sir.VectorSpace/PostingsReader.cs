using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.VectorSpace
{
    /// <summary>
    /// Read (reduce) postings.
    /// </summary>
    public class PostingsReader : Reducer, IPostingsReader
    {
        private readonly ISessionFactory _sessionFactory;
        private readonly ConcurrentDictionary<ulong, Stream> _streams;

        public PostingsReader(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _streams = new ConcurrentDictionary<ulong, Stream>();
        }

        protected override IList<(ulong, long)> Read(ulong collectionId, long keyId, IList<long> offsets)
        {
            var list = new List<(ulong, long)>();

            foreach (var postingsOffset in offsets)
                GetPostingsFromStream(collectionId, keyId, postingsOffset, list);

            return list;
        }

        private void GetPostingsFromStream(ulong collectionId, long keyId, long postingsOffset, IList<(ulong collectionId, long docId)> result)
        {
            var stream = GetOrCreateStream(collectionId, keyId);

            stream.Seek(postingsOffset, SeekOrigin.Begin);

            Span<byte> buf = new byte[sizeof(long)];

            stream.Read(buf);

            var numOfPostings = BitConverter.ToInt64(buf);

            var len = sizeof(long) * (int)numOfPostings;

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
            return _streams.GetOrAdd(
                collectionId,
                _sessionFactory.CreateReadStream(
                    Path.Combine(_sessionFactory.Directory, $"{collectionId}.{keyId}.pos"))
                );
        }

        public void Dispose()
        {
            foreach (var stream in _streams.Values)
                stream.Dispose();
        }
    }
}
