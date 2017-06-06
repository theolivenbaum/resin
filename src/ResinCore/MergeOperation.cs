using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin
{
    public class MergeOperation
    {
        public MergeOperation()
        {
        }

        public long Merge(
            string firstIndexFileName, 
            string secondIndexFileName, 
            string outputDirectory, 
            Compression compression, 
            string primaryKeyFieldName)
        {
            var documents = StreamDocuments(secondIndexFileName)
                .Concat(StreamDocuments(firstIndexFileName));

            var documentStream = new InMemoryDocumentStream(documents);

            var upsert = new UpsertOperation
                (outputDirectory, 
                new Analyzer(), 
                compression, 
                primaryKeyFieldName, 
                documentStream);

            using (upsert)
            {
                return upsert.Write();
            }
        }

        private IEnumerable<Document> StreamDocuments(string ixFileName)
        {
            var dir = Path.GetDirectoryName(ixFileName);
            var ix = IxInfo.Load(ixFileName);
            var docFileName = Path.Combine(dir, ix.VersionId + ".rdoc");
            var docAddressFn = Path.Combine(dir, ix.VersionId + ".da");
            var docHashesFileName = Path.Combine(dir, string.Format("{0}.{1}", ix.VersionId, "pk"));

            return StreamDocuments(
                docFileName, docAddressFn, docHashesFileName, ix.DocumentCount, ix.Compression);
        }

        private IEnumerable<Document> StreamDocuments(
            string docFileName, 
            string docAddressFn, 
            string docHashesFileName, 
            int numOfDocs,
            Compression compression)
        {
            using (var hashReader = new DocHashReader(docHashesFileName))
            using (var addressReader = new DocumentAddressReader(new FileStream(docAddressFn, FileMode.Open, FileAccess.Read)))
            using (var documentReader = new DocumentReader(new FileStream(docFileName, FileMode.Open, FileAccess.Read), compression))
            {
                return StreamDocuments(hashReader, addressReader, documentReader, numOfDocs).ToList();
            }
        }

        private IEnumerable<Document> StreamDocuments(
            DocHashReader hashReader, 
            DocumentAddressReader addressReader, 
            DocumentReader documentReader,
            int numOfDocs)
        {
            for (int docId = 0; docId < numOfDocs; docId++)
            {
                var hash = hashReader.Read(docId);

                var address = addressReader.Read(new[] 
                {
                    new BlockInfo(docId * Serializer.SizeOfBlock(), Serializer.SizeOfBlock())
                }).First();

                var document = documentReader.Read(new List<BlockInfo> { address }).First();

                if (!hash.IsObsolete)
                {
                    yield return document;
                }
            }
        }
    }
}