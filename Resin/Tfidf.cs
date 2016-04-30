using System;
using System.Collections.Generic;

namespace Resin
{
    public class Tfidf : IScoringScheme
    {
        private readonly double _idf;

        public double Idf
        {
            get { return _idf; }
        }

        /// <summary>
        /// Create scoring scheme
        /// </summary>
        public Tfidf()
        {
        }

        /// <summary>
        /// Create scorer
        /// </summary>
        /// <param name="totalNumOfDocs"></param>
        /// <param name="hitCount"></param>
        public Tfidf(int totalNumOfDocs, int hitCount)
        {
            _idf = Math.Log(totalNumOfDocs / (double)hitCount);
        }

        public IScoringScheme CreateScorer(int totalNumOfDocs, int hitCount)
        {
            return new Tfidf(totalNumOfDocs, hitCount);
        }

        public void Score(DocumentScore doc)
        {
            var tf = Math.Sqrt((int)doc.PostingData);
            doc.Score = tf * _idf;
        }

        public void Eval(string field, string value, IAnalyzer analyzer, Dictionary<string, object> postingData)
        {
            var analyze = field[0] != '_';
            var tokens = analyze ? analyzer.Analyze(value) : new[] {value};
            foreach (var token in tokens)
            {
                if (postingData.ContainsKey(token)) postingData[token] = (int)postingData[token] + 1;
                else postingData.Add(token, 1);
            }
        }
    }
}