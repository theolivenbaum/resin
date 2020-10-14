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
        public object Data { get; }
        public Vector<float> Value { get; private set; }
        public int ComponentCount => ((SparseVectorStorage<float>)Value.Storage).ValueCount;

        public IndexedVector(int numOfDimensions)
        {
            Value = CreateVector.Sparse(SparseVectorStorage<float>.OfEnumerable(new float[numOfDimensions]));
        }

        public IndexedVector(IEnumerable<float> values, object data = null)
        {
            Value = CreateVector.Sparse(SparseVectorStorage<float>.OfEnumerable(values));
            Data = data;
        }

        public IndexedVector(SortedList<int, float> dictionary, int numOfDimensions, object data = null)
        {
            var tuples = new Tuple<int, float>[Math.Min(dictionary.Count, numOfDimensions)];
            var i = 0;

            foreach (var p in dictionary)
            {
                if (i == numOfDimensions)
                    break;

                tuples[i++] = new Tuple<int, float>(p.Key, p.Value);
            }

            Value = CreateVector.SparseOfIndexed(numOfDimensions, tuples);
            Data = data;
        }

        public IndexedVector(int[] index, float[] values, int numOfDimensions, object data = null)
        {
            var tuples = new Tuple<int, float>[Math.Min(index.Length, numOfDimensions)];

            for (int i = 0; i < index.Length; i++)
            {
                if (i == numOfDimensions)
                    break;

                tuples[i] = new Tuple<int, float>(index[i], values[i]);
            }

            Value = CreateVector.Sparse(
                SparseVectorStorage<float>.OfIndexedEnumerable(numOfDimensions, tuples));

            Data = data;
        }

        public IndexedVector(Tuple<int, float>[] tuples, int vectorWidth)
        {
            Value = CreateVector.SparseOfIndexed(vectorWidth, tuples);
        }

        public IndexedVector(Vector<float> vector, object data = null)
        {
            Value = vector;
            Data = data;
        }

        public IndexedVector(IEnumerable<IVector> vectors)
        { 
            foreach (var vector in vectors)
            {
                if (Value == null)
                    Value = vector.Value;
                else
                    Value.Add(vector.Value);
            }
        }

        public void Serialize(Stream stream)
        {
            var storage = (SparseVectorStorage<float>)Value.Storage;
            var indices = MemoryMarshal.Cast<int, byte>(storage.Indices);
            var values = MemoryMarshal.Cast<float, byte>(storage.Values);

            stream.Write(indices);
            stream.Write(values);
        }

        public override string ToString()
        {
            return Data == null ? Value.ToString() : Data.ToString();
        }
    }

    public interface IVector
    {
        Vector<float> Value { get; }
        void Serialize(Stream stream);
        int ComponentCount { get; }
        object Data { get; }
    }
}