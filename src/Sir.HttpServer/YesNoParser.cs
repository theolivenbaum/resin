using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sir.HttpServer
{
    /// <summary>
    /// Compiles a string that contains everything from a piece of text that comes after a 
    /// "yes character" while excluding everything that comes after a "no character".
    /// Example use case to extract text from a HTML document:
    /// var parser = new YesNoParser('>', '<');
    /// var freeFromHtmlTags = parser.Parse(html);
    /// </summary>
    public class YesNoParser
    {
        private readonly char _yesChar;
        private readonly char _noChar;
        private readonly IEnumerable<string> _noTags;

        public YesNoParser(char yesChar, char noChar, IEnumerable<string> noTags = null)
        {
            _yesChar = yesChar;
            _noChar = noChar;

            if (noTags == null)
            {
                _noTags = Enumerable.Empty<string>();
            }
            else
            {
                _noTags = noTags;
            }
        }

        public string Parse(string text)
        {
            var skipChars = false;
            var output = new StringBuilder();
            var tag = new StringBuilder();

            foreach(var c in text)
            {
                if (c == _noChar)
                {
                    skipChars = true;
                }
                else if (c == _yesChar)
                {
                    var t = tag.ToString();
                    tag.Clear();

                    var illegalTag = false;

                    foreach(var x in _noTags)
                    {
                        if (t.StartsWith(x))
                        {
                            illegalTag = true;
                        }
                    }

                    if (!illegalTag)
                    {
                        skipChars = false;
                    }
                }
                else
                {
                    if (!skipChars)
                    {
                        output.Append(c);
                    }
                    else
                    {
                        tag.Append(c);
                    }
                }
            }
            return output.ToString();
        }
    }
}
