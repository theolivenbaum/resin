using System;
using System.Linq;

namespace Resin.Analysis
{
    public class LevenshteinDistanceResolver : IDistanceResolver
    {
        private readonly char[] _query;
        private readonly int[] _score;
        private readonly int _maxDistance;

        public LevenshteinDistanceResolver(string query, int maxDistance)
        {
            _query = query.ToCharArray();
            _maxDistance = maxDistance;
            _score = new int[_query.Length];
        }

        public void Put(char c, int depth)
        {
            _score[depth] = _query[depth] == c ? 0 : 1;
        }

        public bool IsValid(char c, int depth)
        {
            var lenDiff = _query.Length - (depth + 1);

            if (depth >= _query.Length)
            {
                return _score.Sum() + (depth + 1 - _query.Length) <= _maxDistance;
            }
            else if (_query[depth] == c)
            {
                _score[depth] = 0;
            }
            else
            {
                _score[depth] = 1;
            }

            var sum = _score.Sum();

            return sum <= _maxDistance;
        }

        public int GetDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
            {
                if (!string.IsNullOrEmpty(b))
                {
                    return b.Length;
                }
                return 0;
            }

            if (string.IsNullOrEmpty(b))
            {
                if (!string.IsNullOrEmpty(a))
                {
                    return a.Length;
                }
                return 0;
            }

            int[,] d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= d.GetUpperBound(0); i += 1)
            {
                d[i, 0] = i;
            }

            for (int i = 0; i <= d.GetUpperBound(1); i += 1)
            {
                d[0, i] = i;
            }

            for (int i = 1; i <= d.GetUpperBound(0); i += 1)
            {
                for (int j = 1; j <= d.GetUpperBound(1); j += 1)
                {
                    var cost = Convert.ToInt32(a[i - 1] != b[j - 1]);

                    var min1 = d[i - 1, j] + 1;
                    var min2 = d[i, j - 1] + 1;
                    var min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }
            return d[d.GetUpperBound(0), d.GetUpperBound(1)];
        }
    }
}