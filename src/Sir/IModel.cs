using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir
{
    public abstract class DistanceCalculator : IDistanceCalculator
    {
        public abstract int VectorWidth { get; }

        public double CosAngle(IVector vec1, IVector vec2)
        {
            //var dotProduct = vec1.Value.DotProduct(vec2.Value);
            //var dotSelf1 = vec1.Value.DotProduct(vec1.Value);
            //var dotSelf2 = vec2.Value.DotProduct(vec2.Value);
            //return (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));

            var dotProduct = vec1.Value.DotProduct(vec2.Value);
            var dotSelf1 = vec1.Value.Norm(2);
            var dotSelf2 = vec2.Value.Norm(2);

            return dotProduct / (dotSelf1 * dotSelf2);
        }

        public double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream)
        {
            Span<byte> buf = new byte[componentCount * 2 * sizeof(float)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(buf);

            var index = MemoryMarshal.Cast<byte, int>(buf.Slice(0, componentCount * sizeof(int)));
            var values = MemoryMarshal.Cast<byte, float>(buf.Slice(componentCount * sizeof(float)));
            var tuples = new Tuple<int, float>[componentCount];

            for (int i = 0; i < componentCount; i++)
            {
                tuples[i] = new Tuple<int, float>(index[i], values[i]);
            }

            var otherVector = CreateVector.SparseOfIndexed(VectorWidth, tuples);

            var dotProduct = vector.Value.DotProduct(otherVector);
            var dotSelf1 = vector.Value.DotProduct(vector.Value);
            var dotSelf2 = otherVector.DotProduct(otherVector);

            return (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }
    }

    /// <summary>
    /// Variable-length tokens become vectors in a word vector space.
    /// </summary>
    public interface IStringModel : IModel<string> {}

    /// <summary>
    /// Rows of pixles from fixed-size images becomes vectors in a image vector space.
    /// </summary>
    public interface IImageModel : IModel<byte[][]> {}

    /// <summary>
    /// Vector space model.
    /// </summary>
    /// <typeparam name="T">The type of data the model should consist of.</typeparam>
    public interface IModel<T> : IModel
    {
        IEnumerable<IVector> Tokenize(T data);
    }

    /// <summary>
    /// Vector space model.
    /// </summary>
    public interface IModel : IVectorSpaceConfig, IDistanceCalculator
    {
    }

    /// <summary>
    /// Vector space configuration.
    /// </summary>
    public interface IVectorSpaceConfig
    {
        double FoldAngle { get; }
        double IdenticalAngle { get; }
    }

    /// <summary>
    /// Calculates the angle between two vectors.
    /// </summary>
    public interface IDistanceCalculator
    {
        int VectorWidth { get; }
        double CosAngle(IVector vec1, IVector vec2);
        double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream);
    }
}
