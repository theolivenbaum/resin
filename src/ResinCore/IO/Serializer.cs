using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Resin.IO.Read;
using log4net;
using StreamIndex;
using DocumentTable;
using Resin.Analysis;

namespace Resin.IO
{
    public static class Serializer
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Serializer));
        public static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly Dictionary<bool, byte> EncodedBoolean = new Dictionary<bool, byte> { { true, 1 }, { false, 0 } };
        public static char SegmentDelimiter = (char)23;

        public static int SizeOfPosting()
        {
            return 2 * sizeof(int);
        }

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

        public static void Serialize(this LcrsTrie trie, Stream stream)
        {
            if (trie.LeftChild != null)
            {
                trie.LeftChild.SerializeDepthFirst(stream);
                LcrsNode.MinValue.Serialize(stream);
            }
        }

        private static void SerializeDepthFirst(this LcrsTrie trie, Stream stream)
        {
            var stack = new Stack<LcrsNode>();
            var node = new LcrsNode(trie, 0, trie.Weight, trie.PostingsAddress);

            while (node != null)
            {
                node.Serialize(stream);

                if (node.Tree.RightSibling != null)
                {
                    stack.Push(new LcrsNode(
                                node.Tree.RightSibling, node.Depth,
                                node.Tree.RightSibling.Weight, node.Tree.RightSibling.PostingsAddress));
                }

                if (node.Tree.LeftChild != null)
                {
                    node = new LcrsNode(
                        node.Tree.LeftChild, (short)(node.Depth + 1),
                        node.Tree.LeftChild.Weight, node.Tree.LeftChild.PostingsAddress);
                }
                else if (stack.Count > 0)
                {
                    node = stack.Pop();
                }
                else
                {
                    break;
                }
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

        public static void Serialize(this AnalyzedTerm term, Stream stream)
        {
            foreach (var position in term.Positions)
            {
                byte[] idBytes = BitConverter.GetBytes(term.DocumentId);
                byte[] dataBytes = BitConverter.GetBytes(position);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(idBytes);
                    Array.Reverse(dataBytes);
                }

                stream.Write(idBytes, 0, sizeof(int));
                stream.Write(dataBytes, 0, sizeof(int));
            }
        }

        //public static int Serialize(this IEnumerable<Posting> postings, Stream stream)
        //{
        //    var size = 0;
        //    foreach (var posting in postings)
        //    {
        //        byte[] idBytes = BitConverter.GetBytes(posting.DocumentId);
        //        byte[] countBytes = BitConverter.GetBytes(posting.Position);

        //        if (!BitConverter.IsLittleEndian)
        //        {
        //            Array.Reverse(idBytes);
        //            Array.Reverse(countBytes);
        //        }

        //        stream.Write(idBytes, 0, sizeof(int));
        //        stream.Write(countBytes, 0, sizeof(int));

        //        size += (idBytes.Length + countBytes.Length);
        //    }
        //    return size;
        //}

        public static void Serialize(this FullTextSegmentInfo ix, string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4, FileOptions.WriteThrough))
            {
                ix.Serialize(fs);
            }
        }

        public static void Serialize(this FullTextSegmentInfo ix, Stream stream)
        {
            var bytes = ix.Serialize();

            stream.Write(bytes, 0, bytes.Length);
        }

        public static byte[] Serialize(this FullTextSegmentInfo ix)
        {
            using (var stream = new MemoryStream())
            {
                byte[] versionBytes = BitConverter.GetBytes(ix.Version);
                byte[] docCountBytes = BitConverter.GetBytes(ix.DocumentCount);
                byte[] compressionEnumBytes = BitConverter.GetBytes((int)ix.Compression);
                byte[] pkFieldNameBytes = ix.PrimaryKeyFieldName == null
                    ? new byte[0]
                    : Encoding.GetBytes(ix.PrimaryKeyFieldName);
                byte[] pkFnLenBytes = BitConverter.GetBytes(pkFieldNameBytes.Length);
                byte[] postingsOffsetBytes = BitConverter.GetBytes(ix.PostingsOffset);
                byte[] docHashOffsetBytes = BitConverter.GetBytes(ix.DocHashOffset);
                byte[] docAddressesOffsetBytes = BitConverter.GetBytes(ix.DocAddressesOffset);
                byte[] keyIndexOffsetBytes = BitConverter.GetBytes(ix.KeyIndexOffset);
                byte[] keyIndexSizeBytes = BitConverter.GetBytes(ix.KeyIndexSize);
                byte[] lenBytes = BitConverter.GetBytes(ix.Length);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(versionBytes);
                    Array.Reverse(docCountBytes);
                    Array.Reverse(compressionEnumBytes);
                    Array.Reverse(pkFieldNameBytes);
                    Array.Reverse(pkFnLenBytes);
                    Array.Reverse(postingsOffsetBytes);
                    Array.Reverse(docHashOffsetBytes);
                    Array.Reverse(docAddressesOffsetBytes);
                    Array.Reverse(keyIndexOffsetBytes);
                    Array.Reverse(keyIndexSizeBytes);
                    Array.Reverse(lenBytes);
                }

                var fieldCountBytes = BitConverter.GetBytes(ix.FieldOffsets.Count);

                stream.Write(fieldCountBytes, 0, sizeof(int));

                ix.FieldOffsets.Serialize(stream);

                stream.Write(versionBytes, 0, sizeof(long));
                stream.Write(docCountBytes, 0, sizeof(int));
                stream.Write(compressionEnumBytes, 0, sizeof(int));
                stream.Write(postingsOffsetBytes, 0, sizeof(long));
                stream.Write(docHashOffsetBytes, 0, sizeof(long));
                stream.Write(docAddressesOffsetBytes, 0, sizeof(long));
                stream.Write(keyIndexOffsetBytes, 0, sizeof(long));
                stream.Write(keyIndexSizeBytes, 0, sizeof(int));
                stream.Write(lenBytes, 0, sizeof(long));

                stream.Write(pkFnLenBytes, 0, sizeof(int));

                if (pkFnLenBytes.Length > 0)
                    stream.Write(pkFieldNameBytes, 0, pkFieldNameBytes.Length);

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

        public static FullTextSegmentInfo DeserializeSegmentInfo(Stream stream)
        {
            //var wordPositions = stream.ReadByte() == 1;

            var fieldCountBytes = new byte[sizeof(int)];

            stream.Read(fieldCountBytes, 0, sizeof(int));

            var fieldCount = BitConverter.ToInt32(fieldCountBytes, 0);

            var fieldOffsets = TableSerializer.DeserializeUlongLongDic(stream, fieldCount).ToDictionary(x => x.Key, y => y.Value);

            var versionBytes = new byte[sizeof(long)];

            stream.Read(versionBytes, 0, sizeof(long));

            var docCountBytes = new byte[sizeof(int)];

            stream.Read(docCountBytes, 0, sizeof(int));

            var compression = new byte[sizeof(int)];

            stream.Read(compression, 0, sizeof(int));

            var postingsOffsetBytes = new byte[sizeof(long)];

            stream.Read(postingsOffsetBytes, 0, sizeof(long));

            var docHashOffsetBytes = new byte[sizeof(long)];

            stream.Read(docHashOffsetBytes, 0, sizeof(long));

            var docAddressesOffsetBytes = new byte[sizeof(long)];

            stream.Read(docAddressesOffsetBytes, 0, sizeof(long));

            var keyIndexOffsetBytes = new byte[sizeof(long)];

            stream.Read(keyIndexOffsetBytes, 0, sizeof(long));

            var keyIndexSizeBytes = new byte[sizeof(int)];

            stream.Read(keyIndexSizeBytes, 0, sizeof(int));

            var lenBytes = new byte[sizeof(long)];

            stream.Read(lenBytes, 0, sizeof(long));

            var pkFnLenBytes = new byte[sizeof(int)];

            stream.Read(pkFnLenBytes, 0, sizeof(int));

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(pkFnLenBytes);
            }

            var pkFnLen = BitConverter.ToInt32(pkFnLenBytes, 0);

            var pkFieldNameBytes = new byte[pkFnLen];

            if (pkFnLen > 0)
                stream.Read(pkFieldNameBytes, 0, pkFieldNameBytes.Length);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(versionBytes);
                Array.Reverse(docCountBytes);
                Array.Reverse(compression);
                Array.Reverse(postingsOffsetBytes);
                Array.Reverse(docHashOffsetBytes);
                Array.Reverse(docAddressesOffsetBytes);
                Array.Reverse(pkFieldNameBytes);
                Array.Reverse(keyIndexOffsetBytes);
                Array.Reverse(keyIndexSizeBytes);
                Array.Reverse(lenBytes);
            }

            var postingsOffset = BitConverter.ToInt64(postingsOffsetBytes, 0);
            var docHashOffset = BitConverter.ToInt64(docHashOffsetBytes, 0);
            var docAddressesOffset = BitConverter.ToInt64(docAddressesOffsetBytes, 0);
            var keyIndexOffset = BitConverter.ToInt64(keyIndexOffsetBytes, 0);
            var keyIndexSize = BitConverter.ToInt32(keyIndexSizeBytes, 0);

            return new FullTextSegmentInfo
            {
                Version = BitConverter.ToInt64(versionBytes, 0),
                DocumentCount = BitConverter.ToInt32(docCountBytes, 0),
                Compression = (Compression)BitConverter.ToInt32(compression, 0),
                PrimaryKeyFieldName = Encoding.GetString(pkFieldNameBytes),
                PostingsOffset = postingsOffset,
                DocHashOffset = docHashOffset,
                DocAddressesOffset = docAddressesOffset,
                FieldOffsets = fieldOffsets,
                KeyIndexOffset = keyIndexOffset,
                KeyIndexSize = keyIndexSize,
                Length = BitConverter.ToInt64(lenBytes, 0),
            };
        }

        public static IList<DocumentPosting> DeserializeTermCounts(Stream stream, int size)
        {
            var count = size / (2 * sizeof(int));
            var postings = new List<DocumentPosting>();
            var buffer = new byte[100 * 2 * sizeof(int)];
            var read = 0;
            var documentId = -1;
            var termCount = 0;

            while (read < count)
            {
                stream.Read(buffer, 0, buffer.Length);

                var bufCount = buffer.Length / (2 * sizeof(int));

                for (int index = 0; index < bufCount; index++)
                {
                    if (read == count) break;

                    var bufIndex = index * 2 * sizeof(int);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(buffer, bufIndex, sizeof(int));
                    }

                    var id = BitConverter.ToInt32(buffer, bufIndex);

                    if (id != documentId)
                    {
                        if (documentId > -1)
                        {
                            postings.Add(
                                new DocumentPosting(documentId, termCount));
                        }
                        documentId = id;
                        termCount = 1;
                    }
                    else
                    {
                        termCount++;
                    }

                    read++;
                }
            }

            postings.Add(new DocumentPosting(documentId, termCount));

            return postings;
        }

        public static IList<DocumentPosting> DeserializePostings(Stream stream, int size)
        {
            var count = size / (2 * sizeof(int));
            var postings = new List<DocumentPosting>(count);
            var buffer = new byte[100 * 2 * sizeof(int)];
            var read = 0;

            while (read < count)
            {
                stream.Read(buffer, 0, buffer.Length);

                var bufCount = buffer.Length / (2 * sizeof(int));

                for (int index = 0; index < bufCount; index++)
                {
                    if (read == count) break;

                    var firstIndex = index * 2 * sizeof(int);
                    var secondIndex = firstIndex + sizeof(int);

                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(buffer, firstIndex, sizeof(int));
                        Array.Reverse(buffer, secondIndex, sizeof(int));
                    }

                    postings.Add(new DocumentPosting(
                        BitConverter.ToInt32(buffer, firstIndex),
                        BitConverter.ToInt32(buffer, secondIndex)
                        ));

                    read++;
                }
            }

            return postings;
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