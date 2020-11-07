﻿using MathNet.Numerics.LinearAlgebra;
using Sir.VectorSpace;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.Search
{
    public abstract class DistanceCalculator : IDistanceCalculator
    {
        public abstract int VectorWidth { get; }

        public double CosAngle(IVector vec1, IVector vec2)
        {
            var dotSelf1 = vec1.Value.Norm(2);
            var dotSelf2 = vec2.Value.Norm(2);
            var dotProduct = vec1.Value.DotProduct(vec2.Value);

            return dotProduct / (dotSelf1 * dotSelf2);
        }

        public double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream, out IVector otherVector)
        {
            Span<byte> buf = new byte[componentCount * 2 * sizeof(int)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(buf);

            var index = MemoryMarshal.Cast<byte, int>(buf.Slice(0, componentCount * sizeof(int)));
            var values = MemoryMarshal.Cast<byte, float>(buf.Slice(componentCount * sizeof(float)));
            var tuples = new Tuple<int, float>[componentCount];

            for (int i = 0; i < componentCount; i++)
            {
                tuples[i] = new Tuple<int, float>(index[i], values[i]);
            }

            otherVector = new IndexedVector(CreateVector.SparseOfIndexed(VectorWidth, tuples));

            var dotSelf1 = vector.Value.Norm(2);
            var dotSelf2 = otherVector.Value.Norm(2);
            var dotProduct = vector.Value.DotProduct(otherVector.Value);

            return dotProduct / (dotSelf1 * dotSelf2);
        }
    }
}