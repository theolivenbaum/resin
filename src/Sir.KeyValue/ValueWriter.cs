using System;
using System.IO;

namespace Sir.KeyValue
{
    /// <summary>
    /// Store values in a stream.
    /// </summary>
    public class ValueWriter : IDisposable
    {
        public Stream Stream { get; }

        public ValueWriter(Stream stream)
        {
            Stream = stream;
        }

        public void Flush()
        {
            Stream.Flush();
        }

        public (long offset, int len, byte dataType) Put(object value)
        {
            var offset = Stream.Position;
            byte dataType;
            int length;

            if (value is bool boolValue)
            {
                Stream.Write(BitConverter.GetBytes(boolValue));
                dataType = DataType.BOOL;
                length = sizeof(bool);
            }
            else if (value is char charValue)
            {
                Stream.Write(BitConverter.GetBytes(charValue));
                dataType = DataType.CHAR;
                length = sizeof(char);
            }
            else if (value is float floatValue)
            {
                Stream.Write(BitConverter.GetBytes(floatValue));
                dataType = DataType.FLOAT;
                length = sizeof(float);
            }
            else if (value is int intValue)
            {
                Stream.Write(BitConverter.GetBytes(intValue));
                dataType = DataType.INT;
                length = sizeof(int);
            }
            else if (value is double doubleValue)
            {
                Stream.Write(BitConverter.GetBytes(doubleValue));
                dataType = DataType.DOUBLE;
                length = sizeof(double);
            }
            else if (value is long longValue)
            {
                Stream.Write(BitConverter.GetBytes(longValue));
                dataType = DataType.LONG;
                length = sizeof(long);
            }
            else if (value is ulong ulongValue)
            {
                Stream.Write(BitConverter.GetBytes(ulongValue));
                dataType = DataType.ULONG;
                length = sizeof(ulong);
            }
            else if (value is DateTime dateTimeValue)
            {
                Stream.Write(BitConverter.GetBytes(dateTimeValue.ToBinary()));
                dataType = DataType.DATETIME;
                length = sizeof(long);
            }
            else if (value is string stringValue)
            {
                var buf = System.Text.Encoding.Unicode.GetBytes(stringValue);
                Stream.Write(buf);
                dataType = DataType.STRING;
                length = buf.Length;
            }
            else if (value is byte byteValue)
            {
                Stream.WriteByte(byteValue);
                dataType = DataType.BYTE;
                length = sizeof(byte);
            }
            else
            {
                var buf = (byte[])value;
                Stream.Write(buf);
                dataType = DataType.STREAM;
                length = buf.Length;
            }

            return (offset, length, dataType);
        }

        public void Dispose()
        {
            Stream.Dispose();
        }
    }
}
