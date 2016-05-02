using System;
using System.Collections.Generic;

namespace Resin.IO
{
    /// <summary>
    /// </summary>
    [Serializable]
    public class PostingsFile : IDentifyable
    {
        /// <summary>
        /// docids/term frequency
        /// </summary>
        private readonly Dictionary<string, object> _postings;
        private readonly string _field;
        private readonly string _token;

        public Dictionary<string, object> Postings { get { return _postings; } }
        public string Field { get { return _field; } }
        public string Token { get { return _token; } }

        public PostingsFile(string field, string token)
        {
            _field = field;
            _token = token;
            _postings = new Dictionary<string, object>();
        }

        public int NumDocs()
        {
            return Postings.Count;
        }

        public string Id { get { return string.Format("{0}.{1}", _field, _token); } }

        public override string ToString()
        {
            return Id;
        }
    }
}