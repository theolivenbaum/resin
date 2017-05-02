using System;
using System.Text;

namespace Resin.Sys
{
    internal static class HashExtensions
    {
        public static UInt32 ToHash(this string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return mm.Hash(bytes);
        }
        private static MurmurHash2UInt32Hack mm = new MurmurHash2UInt32Hack();
    }
}