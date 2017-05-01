using System;
using System.Globalization;

namespace Resin.Sys
{
    internal static class HashExtensions
    {
        public static string ToTokenBasedBucket(this string token)
        {
            var num = (int)token[0] % 100;
            return num.ToString();
        }

        //public static UInt32 ToHash(this string text)
        //{
        //    var bytes = Encoding.UTF8.GetBytes(text);
        //    return new MurmurHash2UInt32Hack().Hash(bytes);
        //}

        /// <summary>
        /// Knuth hash. http://stackoverflow.com/questions/9545619/a-fast-hash-function-for-string-in-c-sharp
        /// One could also use https://msdn.microsoft.com/en-us/library/system.security.cryptography.sha1.aspx
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