using System;
using System.Collections.Generic;

namespace Resin.IO
{
    /// <summary>
    /// </summary>
    [Serializable]
    public class PostingsFile : FileBase<PostingsFile>
    {
        private readonly Dictionary<string, int> _postings;
        /// <summary>
        /// docids/term frequency
        /// </summary>
        public Dictionary<string, int> Postings
        {
            get { return _postings; }
        }

        private readonly string _field;
        public string Field { get { return _field; } }

        public PostingsFile(string field)
        {
            _field = field;
            _postings = new Dictionary<string, int>();
        }

        public int NumDocs()
        {
            return Postings.Count;
        }
    }
}