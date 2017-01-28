using System;
using System.IO;
using System.Text;

namespace Resin.IO
{
    public static class TreeSerializer
    {
        public static void Serialize(this BinaryTree node, string path)
        {
            using(var fs = File.Create(path))
            using (var sw = new StreamWriter(fs, Encoding.Unicode))
            {
                node.LeftChild.Serialize(sw);
            }
        }

        public static void Serialize(this BinaryTree node, StreamWriter sw)
        {
            sw.Write(node.Value);
            sw.Write(node.RightSibling == null ? "0" : "1");
            sw.Write(node.EndOfWord ? "1" : "0");
            sw.Write(Environment.NewLine);

            if (node.LeftChild != null)
            {
                node.LeftChild.Serialize(sw);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.Serialize(sw);
            }
        }
    }
}