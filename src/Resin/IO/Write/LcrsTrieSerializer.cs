using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Resin.IO.Read;

namespace Resin.IO.Write
{
    public static class LcrsTrieSerializer
    {
        public static void SerializeMapped(this LcrsTrie node, string fileName)
        {
            using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs, Encoding.Unicode))
            {
                if (node.LeftChild != null)
                {
                    node.LeftChild.SerializeMappedDepthFirst(bw, 0);
                }
            }
        }
        
        public static void SerializeBinary(this LcrsTrie node, string fileName)
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

        public static void SerializeToTextFile(this LcrsTrie node, string fileName)
        {
            var sb = new StringBuilder();

            if (node.LeftChild != null)
            {
                node.LeftChild.SerializeToTextDepthFirst(sb, 0);
            }

            using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs, Encoding.Unicode))
            {
                sw.Write(sb.ToString());
            } 
        }

        private static void SerializeMappedDepthFirst(this LcrsTrie node, BinaryWriter bw, int depth)
        {
            var bytes = TypeToBytes(new LcrsNode(node, depth, node.GetWeight()));

            bw.Write(bytes, 0, bytes.Length);

            if (node.LeftChild != null)
            {
                node.LeftChild.SerializeMappedDepthFirst(bw, depth + 1);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.SerializeMappedDepthFirst(bw, depth);
            }
        }

        private static void SerializeToTextDepthFirst(this LcrsTrie node, StringBuilder sb, int depth)
        {
            var weight = node.GetWeight();

            sb.Append(node.Value);
            sb.Append(node.RightSibling == null ? "0" : "1");
            sb.Append(node.LeftChild == null ? "0" : "1");
            sb.Append(node.EndOfWord ? "1" : "0");
            sb.Append(depth.ToString(CultureInfo.InvariantCulture).PadRight(10));
            sb.Append(weight.ToString(CultureInfo.InvariantCulture).PadRight(10));

            if (node.LeftChild != null)
            {
                node.LeftChild.SerializeToTextDepthFirst(sb, depth + 1);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.SerializeToTextDepthFirst(sb, depth);
            }
        }

        /// <summary>
        /// http://stackoverflow.com/a/4074557/46645
        /// </summary>
        public static T BytesToType<T>(byte[] bytes)
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return theStructure;
        }

        /// <summary>
        /// http://stackoverflow.com/questions/3278827/how-to-convert-a-structure-to-a-byte-array-in-c
        /// </summary>
        public static byte[] TypeToBytes<T>(T theStructure)
        {
            int size = Marshal.SizeOf(theStructure);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(theStructure, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }
    }
}