using System;
using System.IO;

namespace Sir.KeyValue
{
    /// <summary>
    /// Store values in a stream.
    /// </summary>
    public class ValueWriter : IDisposable
    {
        private readonly Stream _stream;

        public ValueWriter(Stream stream)
        {
            _stream = stream;
        }

        public void Flush()
        {
            _stream.Flush();
        }

        public (long offset, int len, byte dataType) Put(object value)
        {
            var offset = _stream.Position;
            byte dataType;
            int length;

            if (value is bool boolValue)
            {
                _stream.Write(BitConverter.GetBytes(boolValue));
                dataType = DataType.BOOL;
                length = sizeof(bool);
            }
            else if (value is char charValue)
            {
                _stream.Write(BitConverter.GetBytes(charValue));
                dataType = DataType.CHAR;
                length = sizeof(char);
            }
            else if (value is float floatValue)
            {
                _stream.Write(BitConverter.GetBytes(floatValue));
                dataType = DataType.FLOAT;
                length = sizeof(float);
            }
            else if (value is int intValue)
            {
                _stream.Write(BitConverter.GetBytes(intValue));
                dataType = DataType.INT;
                length = sizeof(int);
            }
            else if (value is double doubleValue)
            {
                _stream.Write(BitConverter.GetBytes(doubleValue));
                dataType = DataType.DOUBLE;
                length = sizeof(double);
            }
            else if (value is long longValue)
            {
                _stream.Write(BitConverter.GetBytes(longValue));
                dataType = DataType.LONG;
                length = sizeof(long);
            }
            else if (value is ulong ulongValue)
            {
                _stream.Write(BitConverter.GetBytes(ulongValue));
                dataType = DataType.ULONG;
                length = sizeof(ulong);
            }
            else if (value is DateTime dateTimeValue)
            {
                _stream.Write(BitConverter.GetBytes(dateTimeValue.ToBinary()));
                dataType = DataType.DATETIME;
                length = sizeof(long);
            }
            else if (value is string stringValue)
            {
                var buf = System.Text.Encoding.Unicode.GetBytes(stringValue);
                var compressed = QuickLZ.compress(buf, 1);
                _stream.Write(compressed);
                dataType = DataType.STRING;
                length = compressed.Length;
            }
            else if (value is byte byteValue)
            {
                _stream.WriteByte(byteValue);
                dataType = DataType.BYTE;
                length = sizeof(byte);
            }
            else
            {
                var buf = (byte[])value;
                var compressed = QuickLZ.compress(buf, 1);
                _stream.Write(compressed);
                dataType = DataType.STREAM;
                length = compressed.Length;
            }

            return (offset, length, dataType);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
