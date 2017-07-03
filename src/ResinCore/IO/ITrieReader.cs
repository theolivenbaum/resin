using Resin.Analysis;
using System;
using System.Collections.Generic;

namespace Resin.IO
{
    public interface ITrieReader : IDisposable
    {
        IEnumerable<Word> IsWord(string word);
        IEnumerable<Word> StartsWith(string prefix);
        IEnumerable<Word> SemanticallyNear(string word, int maxEdits, IDistanceResolver distanceResolver = null);
        IEnumerable<Word> Range(string lowerBound, string upperBound);
        bool HasMoreSegments();
    }
}