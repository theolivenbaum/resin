using System;

namespace Resin.IO
{
    [Serializable]
    public class IndexInfo : CompressedFileBase<IndexInfo>
    {
        public DocumentCount DocumentCount { get; set; } 
    }
}