using System;

namespace Resin
{
    public class Analyzer
    {
        private readonly char[] _tokenSeparators;

        public Analyzer(char[] tokenSeparators = null)
        {
            _tokenSeparators = tokenSeparators ?? new[]
            {
                ' ', '.', ',', ';', ':', '!', '"', '&', '?', '#', '*', '+', '|', '=', '-', '_', '@', '\'',
                '<', '>', '“', '”', '´', '`', '(', ')', '[', ']', '{', '}', '/', '\\',
                '\r', '\n', '\t'
            };
        }

        public string[] Analyze(string value)
        {
            return value.ToLowerInvariant().Split(_tokenSeparators, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}