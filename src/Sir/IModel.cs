using System.IO;
using System.IO.MemoryMappedFiles;

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
        IVector DeserializeVector(long vectorOffset, int componentCount, MemoryMappedViewAccessor vectorView);
        long SerializeVector(IVector vector, Stream vectorStream);
        int VectorWidth { get; }
    }

    public interface IEuclidDistance
    {
        double IdenticalAngle0 { get; }
        double FoldAngle0 { get; }
        double IdenticalAngle1 { get; }
        double FoldAngle1 { get; }
        double FoldAngle { get; }
        double IdenticalAngle { get; }
        double CosAngle(IVector vec1, IVector vec2);
        double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream);
    }
}
