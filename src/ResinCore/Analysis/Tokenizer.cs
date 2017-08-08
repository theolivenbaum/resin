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

        public void Tokenize(string value, List<string> tokens)
        {
            var normalized = value.ToLower();

            var washed = new List<char>();

            for (int index = 0; index < normalized.Length; index++)
            {
                var c = normalized[index];

                if (IsData(c))
                {
                    washed.Add(c);
                }
                else
                {
                    if (washed.Count > 0)
                    {
                        tokens.Add(new string(washed.ToArray()));
                        washed.Clear();
                    }
                }
            }
            if (washed.Count > 0)
            {
                tokens.Add(new string(washed.ToArray()));
            }
        }

        private bool IsData(char c)
        {
            if (_customTokenDelimiters == null) return char.IsLetterOrDigit(c);

            return char.IsLetterOrDigit(c) && !_customTokenDelimiters.Contains(c);
        }
    }
}