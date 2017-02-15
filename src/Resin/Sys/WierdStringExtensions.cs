using System;
using System.Globalization;
using Resin.IO;

namespace Resin.Sys
{
    internal static class WierdStringExtensions
    {
        public static string ToPostingsFileId(this Term term)
        {
            if (term == null) throw new ArgumentNullException("term");

            var val = term.Word.Value.PadRight(3).Substring(0, 3);
            return val.ToHash().ToString(CultureInfo.InvariantCulture);
        }

        public static string ToDocFileId(this string docId)
        {
            if (string.IsNullOrEmpty(docId)) throw new ArgumentException("docId");

            var val = docId.PadRight(5).Substring(0, 5);
            return val.ToHash().ToString(CultureInfo.InvariantCulture);
        }

        public static string ToTrieFileId(this string field)
        {
            var fieldHash = field.ToHash().ToString(CultureInfo.InvariantCulture);
            return string.Format("{0}", fieldHash);
        }

        /// <summary>
        /// Knuth hash. http://stackoverflow.com/questions/9545619/a-fast-hash-function-for-string-in-c-sharp
        /// One could also use https://msdn.microsoft.com/en-us/library/system.security.cryptography.sha1.aspx
        /// </summary>
        /// <param name="read"></param>
        /// <returns></returns>
        public static UInt64 ToHash(this string read)
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