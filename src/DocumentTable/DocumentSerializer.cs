using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Resin.Documents
{
    public static class DocumentSerializer
    {
        public static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly Dictionary<bool, byte> EncodedBoolean = new Dictionary<bool, byte> { { true, 1 }, { false, 0 } };

        public static int SizeOfDocHash()
        {
            return sizeof(UInt64) + sizeof(byte);
        }

        public static IDictionary<short, string> ReadKeyIndex(Stream stream, int size)
        {
            var keys = DeserializeStringList(stream, size);
            var keyIndex = new Dictionary<short, string>();

            for (short i = 0; i < keys.Count; i++)
            {
                keyIndex.Add(i, keys[i]);
            }

            return keyIndex;
        }

        public static IList<string> DeserializeStringList(Stream stream, int size)
        {
            var read = 0;
            var strings = new List<string>();

            while (read < size)
            {
                var valLenBytes = new byte[sizeof(short)];

                stream.Read(valLenBytes, 0, sizeof(short));

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

                strings.Add(value);

                read += sizeof(short) + valLen;
            }

            return strings;
        }

        public static IList<DocHash> DeserializeDocHashes(Stream stream)
        {
            var result = new List<DocHash>();

            while (true)
            {
                var hash = DeserializeDocHash(stream);

                if (hash == null) break;

                result.Add(hash);
            }

            return result;
        }

        public static IList<DocHash> DeserializeDocHashes(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DeserializeDocHashes(stream);
            }
        }

        public static DocHash DeserializeDocHash(byte[] buffer)
        {
            var isObsoleteByte = buffer[0];

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer, 1, sizeof(ulong));
            }

            return new DocHash(BitConverter.ToUInt64(buffer, 1), isObsoleteByte == 1);
        }

        public static DocHash DeserializeDocHash(Stream stream)
        {
            var isObsoleteByte = stream.ReadByte();

            if (isObsoleteByte == -1) return null;

            var hashBytes = new byte[sizeof(UInt64)];

            stream.Read(hashBytes, 0, sizeof(UInt64));

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(hashBytes);
            }

            return new DocHash(BitConverter.ToUInt64(hashBytes, 0), isObsoleteByte == 1);
        }

        public static Document DeserializeDocument(Stream stream, int sizeOfDoc, Compression compression, IDictionary<short, string> keyIndex)
        {
            var doc = DeserializeFields(stream, sizeOfDoc, compression, keyIndex);

            return new Document(doc);
        }

        public static IList<Field> DeserializeFields(Stream stream, int size, Compression compression, IDictionary<short, string> keyIndex)
        {
            var read = 0;
            var fields = new List<Field>();

            while (read < size)
            {
                var keyIdBytes = new byte[sizeof(short)];

                stream.Read(keyIdBytes, 0, sizeof(short));

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(keyIdBytes);
                }

                var keyId = BitConverter.ToInt16(keyIdBytes, 0);

                string key = keyIndex[keyId];

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

                string value;

                if (compression == Compression.GZip)
                {
                    value = Encoding.GetString(Deflator.Deflate(valBytes));
                }
                else if (compression == Compression.Lz)
                {
                    value = Encoding.GetString(QuickLZ.decompress(valBytes));
                }
                else
                {
                    value = Encoding.GetString(valBytes);
                }

                read += sizeof(short) + sizeof(int) + valBytes.Length;

                fields.Add(new Field(key, value));
            }
            return fields;
        }

        public static SegmentInfo DeserializeSegmentInfo(Stream stream)
        {
            var fieldCountBytes = new byte[sizeof(int)];

            stream.Read(fieldCountBytes, 0, sizeof(int));

            var fieldCount = BitConverter.ToInt32(fieldCountBytes, 0);

            var fieldOffsets = DeserializeUlongLongDic(stream, fieldCount)
                .ToDictionary(x=>x.Key, y=>y.Value);

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

            return new SegmentInfo
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
                Length = BitConverter.ToInt64(lenBytes, 0)
            };
        }

        public static IEnumerable<KeyValuePair<ulong, long>> DeserializeUlongLongDic(Stream stream, int listCount)
        {
            var maxPos = stream.Position + listCount * (sizeof(ulong) + sizeof(long));

            while (stream.Position < maxPos)
            {
                var keyBytes = new byte[sizeof(ulong)];

                var read = stream.Read(keyBytes, 0, sizeof(ulong));

                if (read == 0) break;

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(keyBytes);
                }

                var key = BitConverter.ToUInt64(keyBytes, 0);

                byte[] valBytes = new byte[sizeof(long)];

                stream.Read(valBytes, 0, sizeof(long));

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valBytes);
                }

                var value = BitConverter.ToInt64(valBytes, 0);

                yield return new KeyValuePair<ulong, long>(key, value);
            }
        }

        public static void Serialize(this IEnumerable<KeyValuePair<ulong, long>> entries, Stream stream)
        {
            foreach (var entry in entries)
            {
                byte[] keyBytes = BitConverter.GetBytes(entry.Key);
                byte[] valBytes = BitConverter.GetBytes(entry.Value);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(keyBytes);
                    Array.Reverse(valBytes);
                }

                stream.Write(keyBytes, 0, sizeof(ulong));
                stream.Write(valBytes, 0, sizeof(long));
            }
        }

        public static int Serialize(this IEnumerable<string> entries, Stream stream)
        {
            var size = 0;
            foreach (var entry in entries)
            {
                byte[] keyBytes = Encoding.GetBytes(entry);
                byte[] lengthBytes = BitConverter.GetBytes((short)keyBytes.Length);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBytes);
                    Array.Reverse(keyBytes);
                }

                stream.Write(lengthBytes, 0, sizeof(short));
                stream.Write(keyBytes, 0, keyBytes.Length);

                size += keyBytes.Length + sizeof(short);
            }
            return size;
        }

        public static void Serialize(this DocHash docHash, Stream stream)
        {
            byte[] hashBytes = BitConverter.GetBytes(docHash.Hash);
            byte isObsoleteByte = EncodedBoolean[docHash.IsObsolete];

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(hashBytes);
            }

            stream.WriteByte(isObsoleteByte);
            stream.Write(hashBytes, 0, sizeof(UInt64));
        }

        public static int Serialize(this IDictionary<short, Field> fields, Compression compression, Stream stream)
        {
            var size = 0;
            foreach (var field in fields)
            {
                byte[] keyBytes = BitConverter.GetBytes(field.Key);
                byte[] valBytes;
                string toStore = field.Value.Store ? field.Value.Value : string.Empty;

                if (compression == Compression.GZip)
                {
                    valBytes = Deflator.Compress(Encoding.GetBytes(toStore));
                }
                else if (compression == Compression.Lz)
                {
                    valBytes = QuickLZ.compress(Encoding.GetBytes(toStore), 1);
                }
                else
                {
                    valBytes = Encoding.GetBytes(toStore);
                }

                byte[] valLengthBytes = BitConverter.GetBytes(valBytes.Length);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(keyBytes);
                    Array.Reverse(valBytes);
                    Array.Reverse(valLengthBytes);
                }

                stream.Write(keyBytes, 0, sizeof(short));
                stream.Write(valLengthBytes, 0, sizeof(int));
                stream.Write(valBytes, 0, valBytes.Length);

                size += (keyBytes.Length + valLengthBytes.Length + valBytes.Length);
            }
            return size;
        }

        public static void Serialize(this IEnumerable<Field> fields, Compression compression, Stream stream)
        {
            foreach (var field in fields)
            {
                byte[] keyBytes = Encoding.GetBytes(field.Key);
                byte[] keyLengthBytes = BitConverter.GetBytes((short)keyBytes.Length);
                byte[] valBytes;
                string toStore = field.Store ? field.Value : string.Empty;

                if (compression == Compression.GZip)
                {
                    valBytes = Deflator.Compress(Encoding.GetBytes(toStore));
                }
                else if (compression == Compression.Lz)
                {
                    valBytes = QuickLZ.compress(Encoding.GetBytes(toStore), 1);
                }
                else
                {
                    valBytes = Encoding.GetBytes(toStore);
                }

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
        }

        public static void Serialize(this SegmentInfo ix, string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4, FileOptions.WriteThrough))
            {
                ix.Serialize(fs);
            }
        }

        public static void Serialize(this SegmentInfo ix, Stream stream)
        {
            var bytes = ix.Serialize();

            stream.Write(bytes, 0, bytes.Length);
        }

        public static byte[] Serialize(this SegmentInfo ix)
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
    }
}
