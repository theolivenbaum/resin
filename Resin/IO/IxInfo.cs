using System;

namespace Resin.IO
{
    [Serializable]
    public class IxInfo : CompressedFileBase<IxInfo>
    {
        public DocumentCount DocumentCount { get; set; }
    }
}