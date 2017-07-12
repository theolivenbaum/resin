using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Resin.IO.Read;
using log4net;
using StreamIndex;
using DocumentTable;

namespace Resin.IO
{
    public static class Serializer
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Serializer));
        public static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly Dictionary<bool, byte> EncodedBoolean = new Dictionary<bool, byte> { { true, 1 }, { false, 0 } };
        public static char SegmentDelimiter = (char)23;

        public static int SizeOfNode()
        {
            return sizeof(char) + 3 * sizeof(byte) + 1 * sizeof(int) + 1 * sizeof(short);
        }

        public static void Serialize(this LcrsTrie trie, string fileName)
        {
            using (var stream = new FileStream(
                    fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                trie.Serialize(stream);
            }
        }

        public static void Serialize(this LcrsTrie trie, Stream treeStream)
        {
            if (trie.LeftChild != null)
            {
                trie.LeftChild.SerializeDepthFirst(treeStream, 0);
                LcrsNode.MinValue.Serialize(treeStream);
            }
        }

        private static void SerializeDepthFirst(
            this LcrsTrie trie, Stream treeStream, short depth)
        {
            new LcrsNode(trie, depth, trie.Weight, trie.PostingsAddress).Serialize(treeStream);

            if (trie.LeftChild != null)
            {
                trie.LeftChild.SerializeDepthFirst(treeStream, (short)(depth + 1));
            }

            if (trie.RightSibling != null)
            {
                trie.RightSibling.SerializeDepthFirst(treeStream, depth);
            }
        }

        public static void Serialize(this LcrsNode node, Stream stream)
        {
            var valBytes = BitConverter.GetBytes(node.Value);
            var haveSiblingByte = EncodedBoolean[node.HaveSibling];
            var haveChildByte = EncodedBoolean[node.HaveChild];
            var eowByte = EncodedBoolean[node.EndOfWord];
            var depthBytes = BitConverter.GetBytes(node.Depth);
            var weightBytes = BitConverter.GetBytes(node.Weight);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(valBytes);
                Array.Reverse(depthBytes);
                Array.Reverse(weightBytes);
            }

            stream.Write(valBytes, 0, valBytes.Length);
            stream.WriteByte(haveSiblingByte);
            stream.WriteByte(haveChildByte);
            stream.WriteByte(eowByte);
            stream.Write(depthBytes, 0, depthBytes.Length);
            stream.Write(weightBytes, 0, weightBytes.Length);

            if (!BlockSerializer.Serialize(node.PostingsAddress, stream) && node.EndOfWord)
            {
                throw new InvalidOperationException("cannot store word without posting address");
            }
        }

        public static byte[] Serialize(this IEnumerable<KeyValuePair<string, int>> entries)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var entry in entries)
                {
                    byte[] keyBytes = Encoding.GetBytes(entry.Key);
                    byte[] lengthBytes = BitConverter.GetBytes((short)keyBytes.Length);
                    byte[] intBytes = BitConverter.GetBytes(entry.Value);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthBytes);
                        Array.Reverse(keyBytes);
                        Array.Reverse(intBytes);
                    }

                    stream.Write(lengthBytes, 0, sizeof(short));
                    stream.Write(keyBytes, 0, keyBytes.Length);
                    stream.Write(intBytes, 0, sizeof(int));
                }
                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this IEnumerable<KeyValuePair<int, string>> entries)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var entry in entries)
                {
                    byte[] valBytes = Encoding.GetBytes(entry.Value);
                    byte[] valLenBytes = BitConverter.GetBytes((short)valBytes.Length);
                    byte[] keyBytes = BitConverter.GetBytes(entry.Key);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(valLenBytes);
                        Array.Reverse(keyBytes);
                        Array.Reverse(valBytes);
                    }

                    stream.Write(keyBytes, 0, sizeof(int));
                    stream.Write(valLenBytes, 0, sizeof(short));
                    stream.Write(valBytes, 0, valBytes.Length);
                }
                return stream.ToArray();
            }
        }

        

        public static byte[] Serialize(this IEnumerable<int> entries)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var entry in entries)
                {
                    byte[] valBytes = BitConverter.GetBytes(entry);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(valBytes);
                    }

                    stream.Write(valBytes, 0, sizeof(int));
                }
                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this IEnumerable<long> entries)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var entry in entries)
                {
                    byte[] valBytes = BitConverter.GetBytes(entry);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(valBytes);
                    }

                    stream.Write(valBytes, 0, sizeof(long));
                }
                return stream.ToArray();
            }
        }

        public static byte[] DeSerializeFile(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ReadToEnd(stream);
            }
        }

        //http://stackoverflow.com/questions/221925/creating-a-byte-array-from-a-stream
        public static byte[] ReadToEnd(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static LcrsNode DeserializeNode(Stream stream)
        {
            var blockArr = new byte[SizeOfNode()];

            stream.Read(blockArr, 0, blockArr.Length);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(blockArr, 0, sizeof(char));
                Array.Reverse(blockArr, sizeof(char), sizeof(short));
                Array.Reverse(blockArr, sizeof(char) + sizeof(short), sizeof(int));
            }

            var val = BitConverter.ToChar(blockArr, 0);
            var haveSibling = BitConverter.ToBoolean(blockArr, sizeof(char));
            var haveChild = BitConverter.ToBoolean(blockArr, sizeof(char) + sizeof(byte));
            var eow = BitConverter.ToBoolean(blockArr, sizeof(char) + 2 * sizeof(byte));
            var depth = BitConverter.ToInt16(blockArr, sizeof(char) + 3 * sizeof(byte));
            var weight = BitConverter.ToInt32(blockArr, sizeof(char) + 3 * sizeof(byte) + sizeof(short));
            var block = BlockSerializer.DeserializeBlock(stream);

            return new LcrsNode(
                val,
                haveSibling,
                haveChild,
                eow,
                depth,
                weight,
                block);
        }

        public static IEnumerable<int> DeserializeIntList(byte[] data)
        {
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(data);
            }

            int pos = 0;

            while (pos < data.Length)
            {
                var val = BitConverter.ToInt32(data, pos);

                yield return val;

                pos = pos + sizeof(int);
            }
        }

        public static IEnumerable<long> DeserializeLongList(string fileName)
        {
            using (var sixStream = new FileStream(
               fileName, FileMode.Open, FileAccess.Read,
               FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                var ms = new MemoryStream();
                sixStream.CopyTo(ms);
                return DeserializeLongList(ms.ToArray()).ToArray();
            }
        }

        public static IEnumerable<long> DeserializeLongList(byte[] data)
        {
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(data);
            }

            int pos = 0;

            while (pos < data.Length)
            {
                var val = BitConverter.ToInt64(data, pos);

                yield return val;

                pos = pos + sizeof(long);
            }
        }

        public static IEnumerable<KeyValuePair<int, string>> DeserializeIntStringDic(Stream stream)
        {
            while (true)
            {
                var keyBytes = new byte[sizeof(int)];

                stream.Read(keyBytes, 0, sizeof(int));

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(keyBytes);
                }

                int key = BitConverter.ToInt32(keyBytes, 0);

                var valLenBytes = new byte[sizeof(short)];

                var read = stream.Read(valLenBytes, 0, sizeof(short));

                if (read == 0) break;

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valLenBytes);
                }

                short valLen = BitConverter.ToInt16(valLenBytes, 0);

                byte[] valBytes = new byte[valLen];

                stream.Read(valBytes, 0, valLen);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valBytes);
                }

                string value = Encoding.GetString(valBytes);

                yield return new KeyValuePair<int, string>(key, value);
            }
        }

        public static IEnumerable<KeyValuePair<string, int>> DeserializeStringIntDic(Stream stream)
        {
            while (true)
            {
                var lengthBytes = new byte[sizeof(short)];

                var read = stream.Read(lengthBytes, 0, sizeof (short));

                if (read == 0) break;

                short keyLength = BitConverter.ToInt16(lengthBytes, 0);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBytes);
                }

                byte[] keyBytes = new byte[keyLength];

                stream.Read(keyBytes, 0, keyLength);

                string key = Encoding.GetString(keyBytes);

                var intBytes = new byte[sizeof(int)];

                stream.Read(intBytes, 0, sizeof (int));

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(intBytes);
                }

                int value = BitConverter.ToInt32(intBytes, 0);
                
                yield return new KeyValuePair<string, int>(key, value);
            }
        }

        public static LcrsTrie DeserializeTrie(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return DeserializeTrie(stream);
            }
        }

        public static LcrsTrie DeserializeTrie(Stream stream)
        {
            using (var reader = new MappedTrieReader(stream))
            {
                return reader.ReadWholeFile();
            }
        }
    }
}