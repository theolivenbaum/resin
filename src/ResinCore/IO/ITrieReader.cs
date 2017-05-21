using Resin.Analysis;
using System.Collections.Generic;

namespace Resin.IO
{
    public interface ITrieReader
    {
        bool HasWord(string word, out Word found);
        IEnumerable<Word> StartsWith(string prefix);
        IEnumerable<Word> Near(string word, int maxEdits, IDistanceResolver distanceResolver);
        IEnumerable<Word> WithinRange(string lowerBound, string upperBound);
    }
}