using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpTest.Net.Serialization;
using Resin.Analysis;
using Resin.IO;
using Resin.Sys;

namespace Resin
{
    public class WriteOperation : Writer
    {
        private readonly IEnumerable<Document> _documents;

        public WriteOperation(string directory, IAnalyzer analyzer, IEnumerable<Document> documents) :base(directory, analyzer)
        {
            _documents = documents;
        }

        protected override IEnumerable<Document> ReadSource()
        {
            return _documents;
        }
    }

    public class DeleteOperation : IDisposable
    {
        private readonly string _directory;
        private readonly IEnumerable<int> _documentIds;
        private readonly string _indexName;

        //TODO: replace with delete by term
        public DeleteOperation(string directory, IEnumerable<int> documentIds)
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

    public class TermSerializer : ISerializer<Term>
    {
        public void WriteTo(Term value, Stream stream)
        {
            PrimitiveSerializer.String.WriteTo(value.Field, stream);
            PrimitiveSerializer.String.WriteTo(value.Word.Value, stream);
        }

        public Term ReadFrom(Stream stream)
        {
            var field = PrimitiveSerializer.String.ReadFrom(stream);
            var word = PrimitiveSerializer.String.ReadFrom(stream);

            return new Term(field, new Word(word));
        }
    }

    public class PostingSerializer : ISerializer<DocumentPosting>
    {
        public void WriteTo(DocumentPosting value, Stream stream)
        {
            PrimitiveSerializer.Int32.WriteTo(value.DocumentId, stream);
            PrimitiveSerializer.Int32.WriteTo(value.Count, stream);
        }

        public DocumentPosting ReadFrom(Stream stream)
        {
            var docId = PrimitiveSerializer.Int32.ReadFrom(stream);
            var count = PrimitiveSerializer.Int32.ReadFrom(stream);

            return new DocumentPosting(docId, count);
        }
    }

    public class ArraySerializer<T> : ISerializer<T[]>
    {
        private readonly ISerializer<T> _itemSerializer;
        public ArraySerializer(ISerializer<T> itemSerializer)
        {
            _itemSerializer = itemSerializer;
        }

        public T[] ReadFrom(Stream stream)
        {
            int size = PrimitiveSerializer.Int32.ReadFrom(stream);
            if (size < 0)
                return null;
            T[] value = new T[size];
            for (int i = 0; i < size; i++)
                value[i] = _itemSerializer.ReadFrom(stream);
            return value;
        }

        public void WriteTo(T[] value, Stream stream)
        {
            if (value == null)
            {
                PrimitiveSerializer.Int32.WriteTo(-1, stream);
                return;
            }
            PrimitiveSerializer.Int32.WriteTo(value.Length, stream);
            foreach (var i in value)
                _itemSerializer.WriteTo(i, stream);
        }
    }
}