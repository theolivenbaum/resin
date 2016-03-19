using System;

namespace Resin
{
    public class Analyzer:IAnalyzer
    {
        public static readonly char[] DefaultTokenSeparators =
        {
            ' ', '.', ',', ';', ':', '!', '"', '&', '?', '#', '*', '+', '|', '=', '-', '_', '@', '\'',
            '<', '>', '“', '”', '´', '`', '(', ')', '[', ']', '{', '}', '/', '\\',
            '\r', '\n', '\t'
        };

        private readonly char[] _tokenSeparators;

        public Analyzer() : this(DefaultTokenSeparators)
        {

        }

        public Analyzer(char[] tokenSeparators)
        {
            if(tokenSeparators==null)
                throw new ArgumentNullException(nameof(tokenSeparators));
            _tokenSeparators = tokenSeparators;
        }

        public string[] Analyze(string value)
        {
            return value.ToLowerInvariant().Split(_tokenSeparators, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}