using System;
using Resin.Documents;

namespace Resin
{
    public class ScoredDocument
    {
        public DocumentTableRow TableRow
        { get; private set; }
        public double Score { get; private set; }

        public ScoredDocument(DocumentTableRow tableRow, double score)
        {
            if (tableRow == null) throw new ArgumentNullException("document");
            TableRow = tableRow;
            Score = score;
        }

        public override string ToString()
        {
            return TableRow.ToString();
        }
    }
}