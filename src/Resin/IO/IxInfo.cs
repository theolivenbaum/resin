using System;

namespace Resin.IO
{
    [Serializable]
    public class IxInfo : CompressedBinaryFile<IxInfo>
    {
        public DocumentCount DocumentCount { get; set; }
    }
}