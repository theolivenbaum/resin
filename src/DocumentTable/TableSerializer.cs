using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DocumentTable
{
    public static class TableSerializer
    {
        public static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly Dictionary<bool, byte> EncodedBoolean = new Dictionary<bool, byte> { { true, 1 }, { false, 0 } };

        public static int SizeOfDocHash()
        {
            return sizeof(UInt64) + sizeof(byte);
        }

        public static int SizeOfPosting()
        {
            return 2 * sizeof(int);
        }

        public static IDictionary<short, string> GetKeyIndex(string kixFileName)
        {
            var keys = ReadKeys(kixFileName);
            var keyIndex = new Dictionary<short, string>();

            for (short i = 0; i < keys.Count; i++)
            {
                keyIndex.Add(i, keys[i]);
            }

            return keyIndex;
        }

        public static IList<string> ReadKeys(string kixFileName)
        {
            using (var fs = File.OpenRead(kixFileName))
            using (var reader = new StreamReader(fs, TableSerializer.Encoding))
            {
                return ReadKeys(reader).ToList();
            }
        }

        public static IEnumerable<string> ReadKeys(StreamReader reader)
        {
            string key;
            while ((key = reader.ReadLine()) != null)
            {
                yield return key;
            }
        }

        public static IEnumerable<DocumentPosting> DeserializePostings(byte[] data)
        {
            var chunk = new byte[SizeOfPosting()];
            int pos = 0;

            while (pos < data.Length)
            {
                Array.Copy(data, pos, chunk, 0, chunk.Length);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(chunk);//ain't gonna work. TODO: reverse individual byte streams, not entire chunk (this is a bug).
                }

                yield return new DocumentPosting(
                    BitConverter.ToInt32(chunk, 0),
                    BitConverter.ToInt32(chunk, sizeof(int))
                    );

                pos = pos + chunk.Length;
            }
        }

        public static IEnumerable<DocHash> DeserializeDocHashes(Stream stream)
        {
            while (true)
            {
                var hash = DeserializeDocHash(stream);

                if (hash == null) break;

                yield return hash.Value;
            }
        }

        public static IList<DocHash> DeserializeDocHashes(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DeserializeDocHashes(stream).ToList();
            }
        }

        public static DocHash? DeserializeDocHash(Stream stream)
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

        public static Document DeserializeDocument(byte[] data, Compression compression, IDictionary<short, string> keyIndex)
        {
            var doc = DeserializeFields(data, compression, keyIndex).ToList();

            return new Document(doc);
        }

        public static IEnumerable<Field> DeserializeFields(byte[] data, Compression compression, IDictionary<short, string> keyIndex)
        {
            using (var stream = new MemoryStream(data))
            {
                return DeserializeFields(stream, compression, keyIndex).ToList();
            }
        }

        public static IEnumerable<Field> DeserializeFields(Stream stream, Compression compression, IDictionary<short, string> keyIndex)
        {
            while (true)
            {
                var keyIdBytes = new byte[sizeof(short)];

                var read = stream.Read(keyIdBytes, 0, sizeof(short));

                if (read == 0) break;

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

                yield return new Field(key, value);
            }
        }

        public static BatchInfo DeserializeBatchInfo(Stream stream)
        {
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
            }

            var postingsOffset = BitConverter.ToInt64(postingsOffsetBytes, 0);
            var docHashOffset = BitConverter.ToInt64(docHashOffsetBytes, 0);
            var docAddressesOffset = BitConverter.ToInt64(docAddressesOffsetBytes, 0);

            return new BatchInfo
            {
                VersionId = BitConverter.ToInt64(versionBytes, 0),
                DocumentCount = BitConverter.ToInt32(docCountBytes, 0),
                Compression = (Compression)BitConverter.ToInt32(compression, 0),
                PrimaryKeyFieldName = Encoding.GetString(pkFieldNameBytes),
                PostingsOffset = postingsOffset,
                DocHashOffset = docHashOffset,
                DocAddressesOffset = docAddressesOffset
            };
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

        public static byte[] Serialize(this DocumentTableRow document, Compression compression)
        {
            using (var stream = new MemoryStream())
            {
                document.Fields.Serialize(compression, stream);

                return stream.ToArray();
            }
        }

        public static void Serialize(this IDictionary<short, Field> fields, Compression compression, Stream stream)
        {
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
            }
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

        public static void Serialize(this BatchInfo ix, string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4, FileOptions.WriteThrough))
            {
                var bytes = ix.Serialize();

                fs.Write(bytes, 0, bytes.Length);
            }
        }

        public static byte[] Serialize(this BatchInfo ix)
        {
            using (var stream = new MemoryStream())
            {
                byte[] versionBytes = BitConverter.GetBytes(ix.VersionId);
                byte[] docCountBytes = BitConverter.GetBytes(ix.DocumentCount);
                byte[] compressionEnumBytes = BitConverter.GetBytes((int)ix.Compression);
                byte[] pkFieldNameBytes = ix.PrimaryKeyFieldName == null
                    ? new byte[0]
                    : Encoding.GetBytes(ix.PrimaryKeyFieldName);
                byte[] pkFnLenBytes = BitConverter.GetBytes(pkFieldNameBytes.Length);
                byte[] postingsOffsetBytes = BitConverter.GetBytes(ix.PostingsOffset);
                byte[] docHashOffsetBytes = BitConverter.GetBytes(ix.DocHashOffset);
                byte[] docAddressesOffsetBytes = BitConverter.GetBytes(ix.DocAddressesOffset);

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
                }

                stream.Write(versionBytes, 0, sizeof(long));
                stream.Write(docCountBytes, 0, sizeof(int));
                stream.Write(compressionEnumBytes, 0, sizeof(int));
                stream.Write(postingsOffsetBytes, 0, sizeof(long));
                stream.Write(docHashOffsetBytes, 0, sizeof(long));
                stream.Write(docAddressesOffsetBytes, 0, sizeof(long));
                stream.Write(pkFnLenBytes, 0, sizeof(int));

                if (pkFnLenBytes.Length > 0) stream.Write(pkFieldNameBytes, 0, pkFieldNameBytes.Length);

                return stream.ToArray();
            }
        }

        public static byte[] Serialize(this IEnumerable<DocumentPosting> postings)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var posting in postings)
                {
                    byte[] idBytes = BitConverter.GetBytes(posting.DocumentId);
                    byte[] countBytes = BitConverter.GetBytes(posting.Count);

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
    }
}
