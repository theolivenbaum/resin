using System.IO;

namespace Sir
{
    /// <summary>
    /// String vector space model.
    /// </summary>
    public interface IStringModel : IModel<string>, IEuclidDistance
    {
    }

    public interface IModel<T>
    {
        AnalyzedData Tokenize(T data);
    }

    public interface IEuclidDistance
    {
        int VectorWidth { get; }
        double IdenticalAngleFirst { get; }
        double FoldAngleFirst { get; }
        double IdenticalAngleSecond { get; }
        double FoldAngleSecond { get; }
        double FoldAngle { get; }
        double IdenticalAngle { get; }
        double FoldAngleNgram { get; }
        double IdenticalAngleNgram { get; }
        double CosAngle(IVector vec1, IVector vec2);
        double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream);
    }
}
