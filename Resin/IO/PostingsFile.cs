using System;
using System.Collections.Generic;

namespace Resin.IO
{
    /// <summary>
    /// </summary>
    [Serializable]
    public class PostingsFile : FileBase<PostingsFile>
    {
        // docids/term frequency
        private readonly Dictionary<string, int> _postings;

        public PostingsFile()  
        {
            _postings = new Dictionary<string, int>();
        }

        public Dictionary<string, int> Postings
        {
            get { return _postings; }
        }

        public int NumDocs()
        {
            return Postings.Count;
        }

        public bool TryGetValue(string docId, out int termFrequency)
        {
            return Postings.TryGetValue(docId, out termFrequency);
        }

        public void AddOrOverwrite(string docId, int termFrequency)
        {
            Postings[docId] = termFrequency;
        }

        public void Remove(string docId)
        {
            Postings.Remove(docId);
        }
    }
}