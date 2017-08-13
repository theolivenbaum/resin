using Resin.Analysis;
using System;
using System.Collections.Generic;

namespace Resin.IO
{
    public interface ITrieReader : IDisposable
    {
        Word IsWord(string word);
        IList<Word> StartsWith(string prefix);
        IList<Word> SemanticallyNear(string word, int maxEdits, IDistanceResolver distanceResolver = null);
        IList<Word> Range(string lowerBound, string upperBound);
    }
}