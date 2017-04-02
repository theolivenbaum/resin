using System;
using System.Globalization;
using System.Text;
using Resin.IO;

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
        public static string ToPostingsFileId(this Term term)
        {
            if (term == null) throw new ArgumentNullException("term");

            var bytes = Encoding.Unicode.GetBytes(term.Word.Value.PadRight(3).Substring(0, 3));
            return new MurmurHash2UInt32Hack().Hash(bytes).ToString(CultureInfo.InvariantCulture);
        }

        public static string ToDocFileId(this string docId)
        {
            if (string.IsNullOrEmpty(docId)) throw new ArgumentException("docId");

            var bytes = Encoding.Unicode.GetBytes(docId.PadRight(3).Substring(0, 3));
            return new MurmurHash2UInt32Hack().Hash(bytes).ToString(CultureInfo.InvariantCulture);
        }

        public static string ToHashString(this string text)
        {
            var bytes = Encoding.Unicode.GetBytes(text);
            return new MurmurHash2UInt32Hack().Hash(bytes).ToString(CultureInfo.InvariantCulture);
        }

        public static UInt32 ToHash(this string text)
        {
            var bytes = Encoding.Unicode.GetBytes(text);
            return new MurmurHash2UInt32Hack().Hash(bytes);
        }
    }
}