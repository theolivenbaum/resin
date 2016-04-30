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

        public Tfidf()
        {
        }

        public Tfidf(int totalNumOfDocs, int hitCount)
        {
            _idf = Math.Log10(totalNumOfDocs / (double)hitCount);
        }


        public Tfidf(double idf)
        {
            _idf = idf;
        }

        public void Score(DocumentScore doc)
        {
            var tf = Math.Sqrt((int)doc.PostingData);
            doc.Score = tf * _idf;
        }

        public void Eval(string field, string value, IAnalyzer analyzer, Dictionary<string, object> postingData)
        {
            var analyze = field[0] != '_';
            if (analyze)
            {
                foreach (var token in analyzer.Analyze(value))
                {
                    if (postingData.ContainsKey(token)) postingData[token] = (int)postingData[token] + 1;
                    else postingData.Add(token, 1);
                }
            }
            else
            {
                if (postingData.ContainsKey(value)) postingData[value] = (int)postingData[value] + 1;
                else postingData.Add(value, 1);
            }
        }
    }

    public interface IScoringScheme
    {
        void Score(DocumentScore doc);
        void Eval(string field, string value, IAnalyzer analyzer, Dictionary<string, object> postingData);
    }
}