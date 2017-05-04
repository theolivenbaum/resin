using Resin.IO;
using System;

namespace Resin.IO
{
    public struct WordInfo : IEquatable<WordInfo>
    {
        public readonly string Field;
        public readonly string Token;
        public readonly DocumentPosting Posting;

        public WordInfo(string field, string token, DocumentPosting posting)
        {
            Field = field;
            Token = token;
            Posting = posting;
        }

        public bool Equals(WordInfo other)
        {
            return other.Field.Equals(Field) && other.Token.Equals(Token);
        }
    }
}