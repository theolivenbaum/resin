using System.IO;
using CSharpTest.Net.Serialization;

namespace Resin.IO
{
    public class ArraySerializer<T> : ISerializer<T[]>
    {
        private readonly ISerializer<T> _itemSerializer;
        public ArraySerializer(ISerializer<T> itemSerializer)
        {
            _itemSerializer = itemSerializer;
        }

        public T[] ReadFrom(Stream stream)
        {
            int size = PrimitiveSerializer.Int32.ReadFrom(stream);
            if (size < 0)
                return null;
            T[] value = new T[size];
            for (int i = 0; i < size; i++)
                value[i] = _itemSerializer.ReadFrom(stream);
            return value;
        }

        public void WriteTo(T[] value, Stream stream)
        {
            if (value == null)
            {
                PrimitiveSerializer.Int32.WriteTo(-1, stream);
                return;
            }
            PrimitiveSerializer.Int32.WriteTo(value.Length, stream);
            foreach (var i in value)
                _itemSerializer.WriteTo(i, stream);
        }
    }
}