using Sir.Documents;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sir.Search
{
    public class DocumentStreamSession : IDisposable
    {
        protected readonly SessionFactory SessionFactory;
        private readonly ConcurrentDictionary<ulong, DocumentReader> _streamReaders;

        public DocumentStreamSession(SessionFactory sessionFactory) 
        {
            SessionFactory = sessionFactory;
            _streamReaders = new ConcurrentDictionary<ulong, DocumentReader>();
        }

        public virtual void Dispose()
        {
            foreach (var reader in _streamReaders.Values)
            {
                reader.Dispose();
            }
        }

        public IEnumerable<Document> ReadDocs(
            ulong collectionId, 
            HashSet<string> select,
            int skip = 0, 
            int take = 0)
        {
            var documentReader = GetOrCreateDocumentReader(collectionId);
            var docCount = documentReader.DocumentCount();

            if (take == 0)
                take = docCount;

            var took = 0;
            long docId = 1 + skip;

            while (docId <= docCount && took < take++)
            {
                yield return ReadDoc((collectionId, docId++), select);
            }
        }

        public IEnumerable<Document> ReadDocs(
            HashSet<string> select,
            DocumentReader documentReader,
            int skip = 0,
            int take = 0)
        {
            var docCount = documentReader.DocumentCount();

            if (take == 0)
                take = docCount;

            var took = 0;
            long docId = 1 + skip;

            while (docId <= docCount && took++ < take)
            {
                yield return ReadDoc((documentReader.CollectionId, docId++), select);
            }
        }

        public Document ReadDoc(
            (ulong collectionId, long docId) docId,
            HashSet<string> select,
            DocumentReader streamReader,
            double? score = null
            )
        {
            var docInfo = streamReader.GetDocumentAddress(docId.docId);
            var docMap = streamReader.GetDocumentMap(docInfo.offset, docInfo.length);
            var fields = new List<Field>();

            for (int i = 0; i < docMap.Count; i++)
            {
                var kvp = docMap[i];
                var kInfo = streamReader.GetAddressOfKey(kvp.keyId);
                var key = (string)streamReader.GetKey(kInfo.offset, kInfo.len, kInfo.dataType);

                if (select.Contains(key))
                {
                    var vInfo = streamReader.GetAddressOfValue(kvp.valId);
                    var val = streamReader.GetValue(vInfo.offset, vInfo.len, vInfo.dataType);

                    fields.Add(new Field(key, val, kvp.keyId, index: select.Contains(key), store: select.Contains(key)));
                }
            }

            return new Document(fields, docId.docId, score.HasValue ? score.Value : 0);
        }

        public Document ReadDoc(
            (ulong collectionId, long docId) docId,
            HashSet<string> select,
            double? score = null
            )
        {
            var streamReader = GetOrCreateDocumentReader(docId.collectionId);

            return ReadDoc(docId, select, streamReader, score);
        }

        private DocumentReader GetOrCreateDocumentReader(ulong collectionId)
        {
            return _streamReaders.GetOrAdd(
                collectionId,
                new DocumentReader(collectionId, SessionFactory)
                );
        }
    }
}