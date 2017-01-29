using System.Diagnostics;

namespace Resin.IO
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public class LcrsNode
    {
        public char Value { get; private set; }
        public bool HasSiblings { get; private set; }
        public bool EndOfWord { get; private set; }
        public int Depth { get; private set; }

        public LcrsNode(string data)
        {
            Value = data[0];
            HasSiblings = data[1] == '1';
            EndOfWord = data[2] == '1';
            Depth = int.Parse(data.Substring(3));
        }
    }
}