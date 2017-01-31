using System.Diagnostics;

namespace Resin.IO.Read
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public class LcrsNode
    {
        public char Value { get; private set; }
        public bool HaveSibling { get; private set; }
        public bool HaveChild { get; private set; }
        public bool EndOfWord { get; private set; }
        public int Depth { get; private set; }

        public LcrsNode(string data)
        {
            Value = data[0];
            HaveSibling = data[1] == '1';
            HaveChild = data[2] == '1';
            EndOfWord = data[3] == '1';
            Depth = int.Parse(data.Substring(4));
        }
    }
}