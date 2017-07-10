using System;
using System.Collections.Generic;
using System.Threading;

namespace Resin.Analysis
{
    public class Tokenizer : ITokenizer
    {
        private readonly HashSet<int> _customTokenDelimiters;
        private static readonly Lazy<Tokenizer> _lazy=new Lazy<Tokenizer>(()=>new Tokenizer(),LazyThreadSafetyMode.PublicationOnly);
        public static  ITokenizer Instance {get { return _lazy.Value; } }

        public Tokenizer(int[] tokenDelimiters = null)
        {
            _customTokenDelimiters = tokenDelimiters == null ? null : new HashSet<int>(tokenDelimiters);
        }

        public void Tokenize(string value, List<(int Start, int Length)> tokens)
        {
            int length = 0, start = 0;

            for (int i = 0; i < value.Length; i++)
            {
                if (IsData(value[i]))
                {
                    length++;
                    continue;
                }

                if (length == 0)
                {
                    start++;
                    continue;
                }

                var w = value.Substring(start, length);
                tokens.Add((start, length));
                start += length + 1;
                length = 0;
            }

            if (length > 0)
            {
                tokens.Add((start, length));
            }
        }

        private bool IsData(char c)
        {
            if (_customTokenDelimiters == null) return char.IsLetterOrDigit(c);

            return char.IsLetterOrDigit(c) && !_customTokenDelimiters.Contains(c);
        }
    }
}