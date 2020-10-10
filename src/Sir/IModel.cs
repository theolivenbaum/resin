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
            var dotSelf1 = vec1.Value.Norm(2);
            var dotSelf2 = vec2.Value.Norm(2);

            if (dotSelf1 == 0 && dotSelf2 > 0)
            {
                return 0;
            }
            else if (dotSelf2 == 0 && dotSelf1 > 0)
            {
                return 0;
            }
            else if (dotSelf1 == 0 && dotSelf2 == 0)
            {
                return 1;
            }

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
                var val = values[i];

                if (val.Approximates(0))
                    val = 0;

                tuples[i] = new Tuple<int, float>(index[i], val);
            }

            otherVector = new IndexedVector(CreateVector.SparseOfIndexed(VectorWidth, tuples));

            var dotSelf1 = vector.Value.Norm(2);
            var dotSelf2 = otherVector.Value.Norm(2);
            var dotProduct = vector.Value.DotProduct(otherVector.Value);

            return dotProduct / (dotSelf1 * dotSelf2);
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
        double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream, out IVector otherVector);
    }
}
