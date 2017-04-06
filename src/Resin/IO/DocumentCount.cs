using System.Collections.Generic;

namespace Resin.IO
{
    public class DocumentCount
    {
        private readonly Dictionary<string, int> _docCount;
        /// <summary>
        /// field/doc count
        /// </summary>
        public Dictionary<string, int> DocCount { get { return _docCount; } }

        public DocumentCount():this(new Dictionary<string,int>())
        {
        }

        public DocumentCount(Dictionary<string, int> docCount)
        {
            _docCount = docCount;
        }
    }
}