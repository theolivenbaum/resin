using System.IO;
using System.Linq;
using System.Text;

namespace Resin.IO.Write
{
    public static class LcrsTreeSerializer
    {
        public static void Serialize(this LcrsTrie node, string fileName)
        {
            var children = node.GetLeftChildAndAllOfItsSiblings().ToList();

            using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs, Encoding.Unicode))
            using (var w = new LcrsTreeBinaryWriter(sw))
            {
                foreach (var child in children)
                {
                    child.RightSibling = null;
                    w.Write(child);
                }
            }
        }

        public static void SerializeDepthFirst(this LcrsTrie node, StringBuilder sb, int depth)
        {
            sb.Append(node.Value);
            sb.Append(node.RightSibling == null ? "0" : "1");
            sb.Append(node.LeftChild == null ? "0" : "1");
            sb.Append(node.EndOfWord ? "1" : "0");
            sb.Append(depth);
            sb.Append('\n');

            if (node.LeftChild != null)
            {
                node.LeftChild.SerializeDepthFirst(sb, depth + 1);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.SerializeDepthFirst(sb, depth);
            }
        }
    }
}