using StreamIndex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.Documents
{
    public class DocumentTableStream : DocumentStream, IDisposable
    {
        private readonly DocHashReader _hashReader;
        private readonly BlockInfoReader _addressReader;
        private readonly DocumentReader _documentReader;
        private readonly SegmentInfo _ix;
        private readonly int _take;
        private readonly int _skip;
        private readonly Stream _dataFile;

        public DocumentTableStream(Stream stream, SegmentInfo ix, int skip = 0, int take = int.MaxValue) 
            : base(ix.PrimaryKeyFieldName)
        {
            _dataFile = stream;
            _ix = ix;
            _dataFile.Seek(_ix.KeyIndexOffset, SeekOrigin.Begin);
            var keyIndex = DocumentSerializer.ReadKeyIndex(_dataFile, _ix.KeyIndexSize);

            _hashReader = new DocHashReader(_dataFile, _ix.DocHashOffset, leaveOpen:false);
            _addressReader = new BlockInfoReader(_dataFile, _ix.DocAddressesOffset);
            _documentReader = new DocumentReader(
                _dataFile, _ix.Compression, keyIndex, _ix.KeyIndexOffset+_ix.KeyIndexSize, leaveOpen:false);

            _skip = skip;
            _take = take;
        }

        public void Dispose()
        {
            _hashReader.Dispose();
            _addressReader.Dispose();
            _documentReader.Dispose();
        }

        public override IEnumerable<Document> ReadSource()
        {
            return ReadSourceAndAssignPk(StreamDocuments());
        }

        private IEnumerable<Document> StreamDocuments()
        {
            var skipped = 0;
            var took = 0;

            for (int docId = 0; docId < _ix.DocumentCount; docId++)
            {
                var hash = _hashReader.Read(docId);

                var address = _addressReader.Read(new[]
                {
                    new BlockInfo(docId * BlockSerializer.SizeOfBlock(), BlockSerializer.SizeOfBlock())
                }).First();

                var document = _documentReader.Read(new List<BlockInfo> { address }).First();

                if (!hash.IsObsolete)
                {
                    if (skipped == _skip && took < _take)
                    {
                        yield return document;
                        took++;
                    }
                    else if (skipped < _skip)
                    {
                        skipped++;
                    }
                    else if (took == _take)
                    {
                        break;
                    }
                }
            }
        }
    }
}
