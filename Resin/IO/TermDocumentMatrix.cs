using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class TermDocumentMatrix
    {
        public Dictionary<Term, List<DocumentWeight>> Weights { get; set; }
    }
}