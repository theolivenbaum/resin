using System.Collections.Generic;

namespace Resin.Analysis
{
    public class AnalyzedTerm
    {
        public string Field { get; private set; }
        public readonly string Value;
        public IList<Posting> Postings { get; private set; }

        public AnalyzedTerm(string key, string value, IList<Posting> postings)
        {
            Field = key;
            Value = value;
            Postings = postings;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}:{2}", Field, Value, Postings.Count);
        }
    }
}