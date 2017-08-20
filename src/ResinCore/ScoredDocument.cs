using System;
using Resin.Documents;

namespace Resin
{
    public class ScoredDocument
    {
        public DocumentTableRow Document { get; private set; }
        public double Score { get; private set; }

        public ScoredDocument(DocumentTableRow document, double score)
        {
            if (document == null) throw new ArgumentNullException("document");
            Document = document;
            Score = score;
        }

        public override string ToString()
        {
            return Document.ToString();
        }
    }
}