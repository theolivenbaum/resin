using System;
using System.Text;

namespace Resin.Sys
{
    internal static class HashExtensions
    {
        /// <summary>
        /// Knuth hash. http://stackoverflow.com/questions/9545619/a-fast-hash-function-for-string-in-c-sharp
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static UInt64 ToHash(this string text)
        {
            UInt64 hashedValue = 3074457345618258791ul;
            for (int i = 0; i < text.Length; i++)
            {
                hashedValue += text[i];
                hashedValue *= 3074457345618258799ul;
            }
            return hashedValue;
        }
    }
}