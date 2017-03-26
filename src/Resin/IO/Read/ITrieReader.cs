using System.Collections.Generic;

namespace Resin.IO.Read
{
    public interface ITrieReader
    {
        bool HasWord(string word, out Word found);
        IEnumerable<Word> StartsWith(string prefix);
        IEnumerable<Word> Near(string word, int maxEdits);
    }
}