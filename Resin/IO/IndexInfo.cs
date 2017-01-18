using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class IndexInfo : CompressedFileBase<IndexInfo>
    {
        public DocumentCount DocumentCount { get; set; }
        public Dictionary<Term, int[]> Postings { get; set; } 
    }
}