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
        IVector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream);
        long SerializeVector(IVector vector, Stream vectorStream);
        int PageWeight { get; }
        int VectorWidth { get; }
    }

    public interface IEuclidDistance
    {
        double PrimaryIdenticalAngle { get; }
        double PrimaryFoldAngle { get; }
        double IdenticalAngle { get; }
        double FoldAngle { get; }
        double CosAngle(IVector vec1, IVector vec2);
        double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream);
    }
}
