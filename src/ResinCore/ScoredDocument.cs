using DocumentTable;
using System;

namespace Resin
{
    public class ScoredDocument
    {
        public Document Document { get; private set; }
        public double Score { get; private set; }

        public ScoredDocument(Document document, double score)
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