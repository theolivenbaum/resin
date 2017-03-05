using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class IxInfo : CompressedBinaryFile<IxInfo>
    {
        public string Name { get; set; }
        public DocumentCount DocumentCount { get; set; }
        public List<string> Deletions { get; set; } 
    }

    [Serializable]
    public class DelInfo : CompressedBinaryFile<DelInfo>
    {
        public string Name { get; set; }
        public List<string> DocIds { get; set; } 
    }
}