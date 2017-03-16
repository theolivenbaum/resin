using System;
using System.Globalization;
using System.Text;
using Resin.IO;

namespace Resin.Sys
{
    internal static class HashExtensions
    {
        public static string ToBucketName(this string token)
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

        public static string ToTrieFileId(this string field)
        {
            var bytes = Encoding.Unicode.GetBytes(field);
            return new MurmurHash2UInt32Hack().Hash(bytes).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Knuth hash. http://stackoverflow.com/questions/9545619/a-fast-hash-function-for-string-in-c-sharp
        /// One could also use https://msdn.microsoft.com/en-us/library/system.security.cryptography.sha1.aspx
        /// </summary>
        /// <param name="read"></param>
        /// <returns></returns>
        public static UInt32 ToHash(this string read)
        {
            var bytes = Encoding.Unicode.GetBytes(read);
            return new MurmurHash2UInt32Hack().Hash(bytes);
        }
    }
}