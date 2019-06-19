using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    /// <summary>
    /// Write session targeting a single collection.
    /// </summary>
    public class WriteSession : DocumentSession, ILogger
    {
        private readonly RocksDb _db;
        private readonly IConfigurationProvider _config;
        private readonly TermIndexSession _indexSession;
        private readonly byte[] _collectionId;

        public WriteSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory,
            TermIndexSession indexSession,
            IConfigurationProvider config,
            RocksDb db) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _indexSession = indexSession;
            _collectionId = BitConverter.GetBytes(CollectionId);
            _db = db;
        }

        public void Commit()
        {
            _indexSession.Commit();
        }

        /// <summary>
        /// Fields prefixed with "___" will not be stored.
        /// </summary>
        /// <returns>Document ID</returns>
        public void Write(IDictionary<string, object> document)
        {
            document["__created"] = DateTime.Now.ToBinary();

            Span<ulong> map = stackalloc ulong[document.Count];
            var docId = Guid.NewGuid().ToByteArray();
            var bigDocId = new BigInteger(docId);
            var off = 0;

            foreach (var key in document.Keys)
            {
                if (key.StartsWith("___"))
                {
                    continue;
                }

                var value = document[key];

                if (value == null)
                {
                    continue;
                }

                var keyId = key.ToHash();
                var keyIdBuf = BitConverter.GetBytes(keyId);

                Put(keyIdBuf, key);

                map[off++] = keyId;

                var valueId = StreamHelper.Concat(docId, keyIdBuf);

                Put(valueId, value);

                var valStr = value as string;

                if (!key.StartsWith("_") && valStr != null)
                {
                    _indexSession.Put(bigDocId, keyId, valStr);
                }
            }

            _db.Put(docId, MemoryMarshal.Cast<ulong, byte>(map).ToArray());
        }

        private void Put(byte[] key, object value)
        {
            Span<byte> buffer;
            byte dataType;

            if (value is bool)
            {
                buffer = BitConverter.GetBytes((bool)value);
                dataType = DataType.BOOL;
            }
            else if (value is char)
            {
                buffer = BitConverter.GetBytes((char)value);
                dataType = DataType.CHAR;
            }
            else if (value is float)
            {
                buffer = BitConverter.GetBytes((float)value);
                dataType = DataType.FLOAT;
            }
            else if (value is int)
            {
                buffer = BitConverter.GetBytes((int)value);
                dataType = DataType.INT;
            }
            else if (value is double)
            {
                buffer = BitConverter.GetBytes((double)value);
                dataType = DataType.DOUBLE;
            }
            else if (value is long)
            {
                buffer = BitConverter.GetBytes((long)value);
                dataType = DataType.LONG;
            }
            else if (value is DateTime)
            {
                buffer = BitConverter.GetBytes(((DateTime)value).ToBinary());
                dataType = DataType.DATETIME;
            }
            else if (value is string)
            {
                buffer = System.Text.Encoding.Unicode.GetBytes((string)value);
                dataType = DataType.STRING;
            }
            else
            {
                buffer = (byte[])value;
                dataType = DataType.STREAM;
            }

            Span<byte> data = stackalloc byte[buffer.Length + 1];

            buffer.CopyTo(data);

            data[data.Length - 1] = dataType;

            _db.Put(key, data.ToArray());
        }

        private void Put(byte[] key, string value)
        {
            var buffer = System.Text.Encoding.Unicode.GetBytes(value);

            _db.Put(key, buffer);
        }
    }

    public static class DbKeys
    {
        public static int DocId = 16;
        public static int KeyId = 8;
        public static int ValueId = 24;
    }
}