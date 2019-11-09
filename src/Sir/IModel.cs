using System.Collections.Generic;
using System.IO;

namespace Sir
{
    /// <summary>
    /// String vector space model.
    /// </summary>
    public interface IStringModel : IModel<string>
    {
    }

    public interface IModel<T> : IEuclidSpace
    {
        IEnumerable<IVector> Tokenize(T data);
    }

    public interface IEuclidSpace : IEuclidDistance
    {
        int VectorWidth { get; }
        double FoldAngle { get; }
        double IdenticalAngle { get; }
    }

    public interface IEuclidDistance
    {
        double CosAngle(IVector vec1, IVector vec2);
        double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream);
    }
}
