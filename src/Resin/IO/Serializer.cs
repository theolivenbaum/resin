using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Resin.IO
{
    public static class Serializer
    {
        private static readonly Encoding Enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly Dictionary<bool, byte> EncodedBoolean = new Dictionary<bool, byte> { { true, 1 }, { false, 0 } };

        public static void Serialize(this IxInfo ix, string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                GraphSerializer.Serializer.Serialize(fs, ix);
            }
        }

        public static void Serialize(this LcrsTrie trie, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4800))
            {
                if (trie.LeftChild != null)
                {
                    trie.LeftChild.SerializeDepthFirst(stream, 0);
                }
            }
        }

        private static void SerializeDepthFirst(this LcrsTrie trie, Stream stream, int depth)
        {
            var bytes = new LcrsNode(trie, depth, trie.GetWeight(), trie.PostingsAddress).Serialize();

            stream.Write(bytes, 0, bytes.Length);

            if (trie.LeftChild != null)
            {
                trie.LeftChild.SerializeDepthFirst(stream, depth + 1);
            }

            if (trie.RightSibling != null)
            {
                trie.RightSibling.SerializeDepthFirst(stream, depth);
            }
        }

        public static int SizeOfNode()
        {
            return sizeof(char) + 3*sizeof (bool) + 2*sizeof (int) + SizeOfBlock();
        }

        public static int SizeOfBlock()
        {
            return sizeof(long) + sizeof(int);
        }

        public static byte[] Serialize(this LcrsNode node)
        {
            using (var stream = new MemoryStream())
            {
                var bytes0 = BitConverter.GetBytes(node.Value);
                var byte0 = EncodedBoolean[node.HaveSibling];
                var byte1 = EncodedBoolean[node.HaveChild];
                var byte2 = EncodedBoolean[node.EndOfWord];
                var bytes1 = BitConverter.GetBytes(node.Depth);
                var bytes2 = BitConverter.GetBytes(node.Weight);
                var bytes3 = Serialize(node.PostingsAddress);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes0);
                    Array.Reverse(bytes1);
                    Array.Reverse(bytes2);
                    Array.Reverse(bytes3);
                }

                stream.Write(bytes0, 0, bytes0.Length);
                stream.WriteByte(byte0);
                stream.WriteByte(byte1);
                stream.WriteByte(byte2);
                stream.Write(bytes1, 0, bytes1.Length);
                stream.Write(bytes2, 0, bytes2.Length);
                stream.Write(bytes3, 0, bytes3.Length);

                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this BlockInfo? block)
        {
            using (var stream = new MemoryStream())
            {
                byte[] longBytes;
                byte[] intBytes;

                if (block == null)
                {
                    longBytes = BitConverter.GetBytes(long.MinValue);
                    intBytes = BitConverter.GetBytes(int.MinValue);
                }
                else
                {
                    longBytes = BitConverter.GetBytes(block.Value.Position);
                    intBytes = BitConverter.GetBytes(block.Value.Length);
                }

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(longBytes);
                    Array.Reverse(intBytes);
                }

                stream.Write(longBytes, 0, longBytes.Length);
                stream.Write(intBytes, 0, intBytes.Length);

                return stream.ToArray();
            }
        }

        public static LcrsNode DeserializeNode(Stream stream)
        {
            if (!stream.CanRead) return LcrsNode.MinValue;

            var bytes0 = new byte[sizeof(char)];
            var bytes1 = new byte[sizeof(int)];
            var bytes2 = new byte[sizeof(int)];

            stream.Read(bytes0, 0, sizeof (char));
            int byte0 = stream.ReadByte();
            int byte1 = stream.ReadByte();
            int byte2 = stream.ReadByte();
            stream.Read(bytes1, 0, sizeof(int));
            stream.Read(bytes2, 0, sizeof(int));
            BlockInfo block = DeserializeBlock(stream);
            
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes0);
                Array.Reverse(bytes1);
                Array.Reverse(bytes2);
            }

            return new LcrsNode(
                BitConverter.ToChar(bytes0, 0),
                byte0 == 1,
                byte1 == 1,
                byte2 == 1,
                BitConverter.ToInt32(bytes1, 0),
                BitConverter.ToInt32(bytes2, 0),
                block);
        }

        public static BlockInfo DeserializeBlock(Stream stream)
        {
            var longBytes = new byte[sizeof(long)];
            var intBytes = new byte[sizeof(int)];

            stream.Read(longBytes, 0, sizeof (long));
            stream.Read(intBytes, 0, sizeof (int));

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(longBytes);
                Array.Reverse(intBytes);
            }

            return new BlockInfo(BitConverter.ToInt64(longBytes, 0), BitConverter.ToInt32(intBytes, 0));
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