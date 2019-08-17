using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir
{
    public class IndexedVector : IVector
    {
        public Memory<char>? Data { get; }
        public Vector<float> Value { get; private set; }
        public int ComponentCount { get; }

        public IndexedVector(SortedList<int, float> dictionary, Memory<char> data, int vectorWidth)
        {
            var tuples = new Tuple<int, float>[dictionary.Count];

            var i = 0;

            foreach (var p in dictionary)
            {
                tuples[i++] = new Tuple<int, float>(p.Key, p.Value);
            }

            Value = CreateVector.SparseOfIndexed(vectorWidth, tuples);

            ComponentCount = tuples.Length;
            Data = data;
        }

        public IndexedVector(int[] index, float[] values, int vectorWidth)
        {
            var tuples = new Tuple<int, float>[index.Length];

            for (int i = 0; i < index.Length; i++)
            {
                tuples[i] = new Tuple<int, float>(index[i], values[i]);
            }

            Value = CreateVector.Sparse(
                SparseVectorStorage<float>.OfIndexedEnumerable(vectorWidth, tuples));

            ComponentCount = tuples.Length;
        }

        public IndexedVector(Tuple<int, float>[] tuples, int vectorWidth)
        {
            Value = CreateVector.SparseOfIndexed(vectorWidth, tuples);
            ComponentCount = tuples.Length;
        }

        public IndexedVector(Vector<float> vector)
        {
            Value = vector;
            ComponentCount = ((SparseVectorStorage<float>)Value.Storage).Length;
        }

        public void Serialize(Stream stream)
        {
            stream.Write(MemoryMarshal.Cast<int, byte>(((SparseVectorStorage<float>)Value.Storage).Indices));
            stream.Write(MemoryMarshal.Cast<float, byte>(((SparseVectorStorage<float>)Value.Storage).Values));
        }

        public override string ToString()
        {
            return Data.HasValue ? new string(Data.Value.ToArray()) : Value.ToString();
        }
    }

    public interface IVector
    {
        Vector<float> Value { get; }
        void Serialize(Stream stream);
        int ComponentCount { get; }
        Memory<char>? Data { get; }
    }
}