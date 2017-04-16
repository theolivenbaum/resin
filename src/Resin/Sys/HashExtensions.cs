using System;
using System.Globalization;
using System.Text;

namespace Resin.Sys
{
    internal static class HashExtensions
    {
        public static string ToTrieBucketName(this string token)
        {
            for (int index = 0; index < token.Length; index++)
            {
                var c = token[index];

                if (c > 127)
                {
                    if (c > 255)
                    {
                        return "256";
                    }
                    return "128";
                }
            }
            
            return ((int)token[0]).ToString(CultureInfo.InvariantCulture);
        }

        public static string ToHashString(this string text)
        {
            return text.ToHash().ToString(CultureInfo.InvariantCulture);
        }

        public static UInt32 ToHash(this string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return new MurmurHash2UInt32Hack().Hash(bytes);
        }
    }
}