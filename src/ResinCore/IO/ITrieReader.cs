using Resin.Analysis;
using System;
using System.Collections.Generic;

namespace Resin.IO
{
    public interface ITrieReader : IDisposable
    {
        IEnumerable<Word> IsWord(string word);
        IEnumerable<Word> StartsWith(string prefix);
        IEnumerable<Word> Near(string word, int maxEdits, IDistanceAutomaton distanceResolver = null);
        IEnumerable<Word> WithinRange(string lowerBound, string upperBound);
        bool HasMoreSegments();
    }
}