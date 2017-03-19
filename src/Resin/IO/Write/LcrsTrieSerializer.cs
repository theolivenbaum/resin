using System;
using System.IO;
using System.Runtime.InteropServices;
using Resin.IO.Read;

namespace Resin.IO.Write
{
    public static class LcrsTrieSerializer
    {
        public static void SerializeMapped(this LcrsTrie node, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4800))
            {
                if (node.LeftChild != null)
                {
                    node.LeftChild.SerializeMappedDepthFirst(stream, 0);
                }
            }
        }

        private static void SerializeMappedDepthFirst(this LcrsTrie node, Stream stream, int depth)
        {
            var bytes = TypeToBytes(new LcrsNode(node, depth, node.GetWeight()));
            stream.Write(bytes, 0, bytes.Length);

            if (node.LeftChild != null)
            {
                node.LeftChild.SerializeMappedDepthFirst(stream, depth + 1);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.SerializeMappedDepthFirst(stream, depth);
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