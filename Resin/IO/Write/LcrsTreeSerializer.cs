using System.IO;
using System.Text;

namespace Resin.IO.Write
{
    public static class LcrsTreeSerializer
    {
        public static void Serialize(this LcrsTrie node, string path)
        {
            using(var fs = File.Create(path))
            using (var sw = new StreamWriter(fs, Encoding.Unicode))
            {
                node.LeftChild.Serialize(sw, 0);
            }
        }

        public static void Serialize(this LcrsTrie node, StreamWriter sw, int depth)
        {
            sw.Write(node.Value);
            sw.Write(node.RightSibling == null ? "0" : "1");
            sw.Write(node.LeftChild == null ? "0" : "1");
            sw.Write(node.EndOfWord ? "1" : "0");
            sw.Write(depth);
            sw.Write('\n');

            if (node.LeftChild != null)
            {
                node.LeftChild.Serialize(sw, depth + 1);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.Serialize(sw, depth);
            }
        }
    }
}