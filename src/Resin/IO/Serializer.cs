using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Resin.IO
{
    public static class Serializer
    {
        public static void Serialize(this IxInfo ix, string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, Encoding.Unicode))
            {
                writer.WriteLine(ix.Name);

                foreach (var field in ix.DocumentCount)
                {
                    writer.WriteLine(field.Key);
                    writer.WriteLine(field.Value);
                }
            }
        }
        public static void SerializeMapped(this LcrsTrie trie, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4800))
            {
                if (trie.LeftChild != null)
                {
                    trie.LeftChild.SerializeMappedDepthFirst(stream, 0);
                }
            }
        }

        private static void SerializeMappedDepthFirst(this LcrsTrie trie, Stream stream, int depth)
        {
            var bytes = TypeToBytes(new LcrsNode(trie, depth, trie.GetWeight(), trie.PostingsAddress));
            stream.Write(bytes, 0, bytes.Length);

            if (trie.LeftChild != null)
            {
                trie.LeftChild.SerializeMappedDepthFirst(stream, depth + 1);
            }

            if (trie.RightSibling != null)
            {
                trie.RightSibling.SerializeMappedDepthFirst(stream, depth);
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