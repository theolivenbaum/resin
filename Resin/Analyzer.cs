using System;
using System.Linq;

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
            if (tokenSeparators == null) throw new ArgumentNullException("tokenSeparators");
            _tokenSeparators = tokenSeparators;
        }

        public string[] Analyze(string value) // TODO: could be made lazy
        {
            var analyzed = value.ToLowerInvariant().Split(_tokenSeparators);
            var cleansed = analyzed.Select(t=>t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray(); // I have no idea why I have to be trimming. I just split by 'space'. Wierd.
            return cleansed;
        }
    }
}