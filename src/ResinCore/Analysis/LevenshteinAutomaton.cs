using System.Collections.Generic;
using System.Linq;

namespace Resin.Analysis
{
    public class LevenshteinAutomaton : IDistanceAutomaton
    {
        private readonly char[] _query;
        private readonly int[] _score;
        private readonly int _maxDistance;

        public LevenshteinAutomaton(string query, int maxDistance)
        {
            _query = query.ToCharArray();
            _maxDistance = maxDistance;
            _score = new int[_query.Length];
            //for (int i = 0; i < _query.Length; i++)
            //{
            //    _score[i] = 0;
            //}
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
    }
}
