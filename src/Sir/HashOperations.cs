using System;

namespace Sir
{
    public static class BinaryHelper
    {
        public static ulong ToHash(this string text)
        {
            return ToHash((IComparable)text);
        }

        public static ulong ToHash(this IComparable text)
        {
            return CalculateKnuthHash(text.ToString());
        }

        private static ulong CalculateKnuthHash(string read)
        {
            UInt64 hashedValue = 3074457345618258791ul;
            for (int i = 0; i < read.Length; i++)
            {
                hashedValue += read[i];
                hashedValue *= 3074457345618258799ul;
            }
            return hashedValue;
        }
    }
}

