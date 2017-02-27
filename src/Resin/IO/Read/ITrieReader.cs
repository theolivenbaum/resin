using System.Collections.Generic;

namespace Resin.IO.Read
{
    public interface ITrieReader
    {
        bool HasWord(string word);
        IEnumerable<Word> StartsWith(string prefix);
        IEnumerable<Word> Near(string word, int edits);
    }
}