using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Resin.IO.Read;
using Resin.Sys;

namespace Resin.IO
{
    public static class Serializer
    {
        public static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly Dictionary<bool, byte> EncodedBoolean = new Dictionary<bool, byte> { { true, 1 }, { false, 0 } };

        public static int SizeOfNode()
        {
            return sizeof(char) + 3 * sizeof(bool) + 1 * sizeof(int) + 1 * sizeof(short) + SizeOfBlock();
        }

        public static int SizeOfBlock()
        {
            return 1 * sizeof(long) + 1 * sizeof(int);
        }

        public static void Serialize(this IxInfo ix, string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = ix.Serialize();

                fs.Write(bytes, 0, bytes.Length);
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

        private static void SerializeDepthFirst(this LcrsTrie trie, Stream stream, short depth)
        {
            var bytes = new LcrsNode(trie, depth, trie.GetWeight(), trie.PostingsAddress).Serialize();

            stream.Write(bytes, 0, bytes.Length);

            if (trie.LeftChild != null)
            {
                trie.LeftChild.SerializeDepthFirst(stream, (short)(depth + 1));
            }

            if (trie.RightSibling != null)
            {
                trie.RightSibling.SerializeDepthFirst(stream, depth);
            }
        }

        public static byte[] Serialize(this LcrsNode node)
        {
            using (var stream = new MemoryStream())
            {
                var valBytes = BitConverter.GetBytes(node.Value);
                var byte0 = EncodedBoolean[node.HaveSibling];
                var byte1 = EncodedBoolean[node.HaveChild];
                var byte2 = EncodedBoolean[node.EndOfWord];
                var depthBytes = BitConverter.GetBytes(node.Depth);
                var weightBytes = BitConverter.GetBytes(node.Weight);
                var addrBytes = Serialize(node.PostingsAddress);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valBytes);
                    Array.Reverse(depthBytes);
                    Array.Reverse(weightBytes);
                    Array.Reverse(addrBytes);
                }

                stream.Write(valBytes, 0, valBytes.Length);
                stream.WriteByte(byte0);
                stream.WriteByte(byte1);
                stream.WriteByte(byte2);
                stream.Write(depthBytes, 0, depthBytes.Length);
                stream.Write(weightBytes, 0, weightBytes.Length);
                stream.Write(addrBytes, 0, addrBytes.Length);

                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this BlockInfo? block)
        {
            using (var stream = new MemoryStream())
            {
                if (block == null)
                {
                    var min = BitConverter.GetBytes(int.MinValue);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(min);
                    }

                    stream.Write(min, 0, min.Length);
                    stream.Write(min, 0, min.Length);
                }
                else
                {
                    var blockBytes = block.Value.Serialize();

                    stream.Write(blockBytes, 0, blockBytes.Length);
                }
                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this Document document, bool compress)
        {
            using (var stream = new MemoryStream())
            {
                var idBytes = BitConverter.GetBytes(document.Id);
                var dicBytes = document.Fields.Serialize(compress);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(idBytes);
                    Array.Reverse(dicBytes);
                }

                stream.Write(idBytes, 0, idBytes.Length);
                stream.Write(dicBytes, 0, dicBytes.Length);

                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this BlockInfo block)
        {
            using (var stream = new MemoryStream())
            {
                byte[] posBytes = BitConverter.GetBytes(block.Position);
                byte[] lenBytes = BitConverter.GetBytes(block.Length);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(posBytes);
                    Array.Reverse(lenBytes);
                }

                stream.Write(posBytes, 0, posBytes.Length);
                stream.Write(lenBytes, 0, lenBytes.Length);

                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this IxInfo ix)
        {
            using (var stream = new MemoryStream())
            {
                byte[] nameBytes = Encoding.GetBytes(ix.VersionId);
                byte[] lengthBytes = BitConverter.GetBytes((short)nameBytes.Length);
                byte[] dicBytes = ix.DocumentCount.Serialize();
                byte[] docIdBytes = BitConverter.GetBytes(ix.NextDocId);
                byte[] startDocIdBytes = BitConverter.GetBytes(ix.StartDocId);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(nameBytes);
                    Array.Reverse(lengthBytes);
                    Array.Reverse(dicBytes);
                    Array.Reverse(docIdBytes);
                    Array.Reverse(startDocIdBytes);
                }

                stream.Write(startDocIdBytes, 0, sizeof(int));
                stream.Write(docIdBytes, 0, sizeof(int));
                stream.Write(lengthBytes, 0, sizeof(short));
                stream.Write(nameBytes, 0, nameBytes.Length);
                stream.Write(dicBytes, 0, dicBytes.Length);

                return stream.ToArray();
            }
        }

        public static IxInfo DeserializeIxInfo(Stream stream)
        {
            var startDocIdBytes = new byte[sizeof(int)];

            stream.Read(startDocIdBytes, 0, sizeof(int));

            var docIdBytes = new byte[sizeof(int)];

            stream.Read(docIdBytes, 0, sizeof(int));

            var lengthBytes = new byte[sizeof(short)];

            stream.Read(lengthBytes, 0, sizeof(short));

            var stringLength = BitConverter.ToInt16(lengthBytes, 0);

            var stringBytes = new byte[stringLength];

            stream.Read(stringBytes, 0, stringLength);

            var dic = DeserializeStringIntDic(stream).ToList();

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(stringBytes);
                Array.Reverse(lengthBytes);
                Array.Reverse(docIdBytes);
                Array.Reverse(startDocIdBytes);
            }

            return new IxInfo
            {
                VersionId= Encoding.GetString(stringBytes), 
                DocumentCount = dic.ToDictionary(x=>x.Key, x=>x.Value),
                StartDocId = BitConverter.ToInt32(startDocIdBytes, 0),
                NextDocId = BitConverter.ToInt32(docIdBytes, 0)
            };
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

        public static byte[] Serialize(this IEnumerable<KeyValuePair<string, string>> entries, bool compress)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var entry in entries)
                {
                    byte[] keyBytes = Encoding.GetBytes(entry.Key);
                    byte[] keyLengthBytes = BitConverter.GetBytes((short)keyBytes.Length);
                    byte[] valBytes = compress ? Compressor.Compress((entry.Value ?? string.Empty)) : Encoding.GetBytes((entry.Value ?? string.Empty));
                    byte[] valLengthBytes = BitConverter.GetBytes(valBytes.Length);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(keyLengthBytes);
                        Array.Reverse(keyBytes);
                        Array.Reverse(valBytes);
                        Array.Reverse(valLengthBytes);
                    }

                    stream.Write(keyLengthBytes, 0, sizeof(short));
                    stream.Write(keyBytes, 0, keyBytes.Length);
                    stream.Write(valLengthBytes, 0, sizeof(int));
                    stream.Write(valBytes, 0, valBytes.Length);
                }
                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this IEnumerable<DocumentPosting> entries)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var entry in entries)
                {
                    byte[] idBytes = BitConverter.GetBytes(entry.DocumentId);
                    byte[] countBytes = BitConverter.GetBytes(entry.Count);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(idBytes);
                        Array.Reverse(countBytes);
                    }

                    stream.Write(idBytes, 0, sizeof(int));
                    stream.Write(countBytes, 0, sizeof(int));
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

        public static LcrsNode DeserializeNode(Stream stream)
        {
            if (!stream.CanRead) return LcrsNode.MinValue;

            var valBytes = new byte[sizeof(char)];
            var depthBytes = new byte[sizeof(short)];
            var weightBytes = new byte[sizeof(int)];

            stream.Read(valBytes, 0, sizeof (char));
            int byte0 = stream.ReadByte();
            int byte1 = stream.ReadByte();
            int byte2 = stream.ReadByte();
            stream.Read(depthBytes, 0, depthBytes.Length);
            stream.Read(weightBytes, 0, weightBytes.Length);
            BlockInfo? block = DeserializeBlock(stream);
            
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(valBytes);
                Array.Reverse(depthBytes);
                Array.Reverse(weightBytes);
            }

            return new LcrsNode(
                BitConverter.ToChar(valBytes, 0),
                byte0 == 1,
                byte1 == 1,
                byte2 == 1,
                BitConverter.ToInt16(depthBytes, 0),
                BitConverter.ToInt32(weightBytes, 0),
                block);
        }

        public static BlockInfo? DeserializeBlock(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DeserializeBlock(stream);
            }
        }

        public static Document DeserializeDocument(byte[] data, bool wasCompressed)
        {
            var idBytes = new byte[sizeof(int)];
            Array.Copy(data, 0, idBytes, 0, sizeof(int));

            var dicBytes = new byte[data.Length - sizeof(int)];
            Array.Copy(data, sizeof(int), dicBytes, 0, dicBytes.Length);
            
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(idBytes);
                Array.Reverse(dicBytes);
            }

            var id = BitConverter.ToInt32(idBytes, 0);
            var dic = DeserializeStringStringDic(dicBytes, wasCompressed).ToDictionary(x=>x.Key, y=>y.Value);

            return new Document(dic) {Id = id};
        }

        public static BlockInfo DeserializeBlock(Stream stream)
        {
            var posBytes = new byte[sizeof(long)];
            var lenBytes = new byte[sizeof(int)];

            stream.Read(posBytes, 0, posBytes.Length);
            stream.Read(lenBytes, 0, lenBytes.Length);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(posBytes);
                Array.Reverse(lenBytes);
            }

            return new BlockInfo(BitConverter.ToInt32(posBytes, 0), BitConverter.ToInt32(lenBytes, 0));
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

        public static IEnumerable<DocumentPosting> DeserializePostings(byte[] data)
        {
            var chunk = new byte[sizeof(int)*2];
            long pos = 0;

            while (pos<data.Length)
            {
                Array.Copy(data, pos, chunk, 0, chunk.Length);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(chunk);
                }

                yield return new DocumentPosting(
                    BitConverter.ToInt32(chunk, 0),
                    BitConverter.ToInt32(chunk, sizeof(int)));

                pos = pos + chunk.Length;
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> DeserializeStringStringDic(byte[] data, bool wasCompressed)
        {
            using (var stream = new MemoryStream(data))
            {
                return DeserializeStringStringDic(stream, wasCompressed).ToList();
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> DeserializeStringStringDic(Stream stream, bool wasCompressed)
        {
            while (true)
            {
                var keyLengthBytes = new byte[sizeof(short)];

                var read = stream.Read(keyLengthBytes, 0, sizeof(short));

                if (read == 0) break;

                short keyLength = BitConverter.ToInt16(keyLengthBytes, 0);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(keyLengthBytes);
                }

                byte[] keyBytes = new byte[keyLength];

                stream.Read(keyBytes, 0, keyLength);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(keyBytes);
                }

                string key = Encoding.GetString(keyBytes);

                var valLengthBytes = new byte[sizeof(int)];

                stream.Read(valLengthBytes, 0, sizeof(int));

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valLengthBytes);
                }

                int valLength = BitConverter.ToInt32(valLengthBytes, 0);

                byte[] valBytes = new byte[valLength];

                stream.Read(valBytes, 0, valLength);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valBytes);
                }

                string value = wasCompressed ? Compressor.DecompressText(valBytes) : Encoding.GetString(valBytes);

                yield return new KeyValuePair<string, string>(key, value);
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

        public static LcrsTrie DeserializeTrie(string directory, string indexVersionId, string field)
        {
            var searchPattern = string.Format("{0}-{1}-*", indexVersionId, field.ToHashString());

            return DeserializeTrie(directory, searchPattern);
        }

        public static LcrsTrie DeserializeTrie(string directory, string searchPattern)
        {
            var root = new LcrsTrie('\0', false);
            LcrsTrie next = null;

            foreach (var fileName in Directory.GetFiles(directory, searchPattern).OrderBy(f => f))
            {
                using (var reader = new MappedTrieReader(fileName))
                {
                    var trie = reader.ReadWholeFile();

                    if (next == null)
                    {
                        root.LeftChild = trie;
                    }
                    else
                    {
                        next.RightSibling = trie;
                    }
                    next = trie;
                }
            }

            return root;
        }

        public static LcrsTrie DeserializeTrie(string fileName)
        {
            using (var reader = new MappedTrieReader(fileName))
            {
                var root = new LcrsTrie('\0', false);
                var trie = reader.ReadWholeFile();

                root.LeftChild = trie;

                return root;
            }
        }
    }
}