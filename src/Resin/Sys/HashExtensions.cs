using System;
using System.Globalization;
using System.Text;
using Resin.IO;

namespace Resin.Sys
{
    internal static class HashExtensions
    {
        public static string ToBucketName(this char c)
        {
            if (c > 47 && c < 128)
            {
                return ((int)c).ToString();
            }
            
            return "128";
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