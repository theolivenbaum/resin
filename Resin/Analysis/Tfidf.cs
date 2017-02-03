using System;
using System.Collections.Generic;
using Resin.Querying;

namespace Resin.Analysis
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
        /// <param name="docsInCorpus"></param>
        /// <param name="docsWithTerm"></param>
        public Tfidf(int docsInCorpus, int docsWithTerm)
        {
            _idf = Math.Log(docsInCorpus / (double)docsWithTerm + 1) + 1;
        }

        public IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm)
        {
            return new Tfidf(docsInCorpus, docsWithTerm);
        }

        public void Score(DocumentScore doc)
        {
            var tf = Math.Sqrt(doc.TermCount);
            var k = 1 / (float)(1 + (doc.Distance*2));
            var augTf = tf*k;
            doc.Score = augTf * _idf;
        }

        public void Analyze(string field, string value, IAnalyzer analyzer, Dictionary<string, int> termCount)
        {
            var analyze = field[0] != '_';
            var tokens = analyze ? analyzer.Analyze(value) : new[] {value};
            foreach (var token in tokens)
            {
                if (termCount.ContainsKey(token)) termCount[token] = termCount[token] + 1;
                else termCount.Add(token, 1);
            }
        }
    }
}