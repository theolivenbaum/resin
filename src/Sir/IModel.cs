using System.IO;

namespace Sir
{
    /// <summary>
    /// String vector space model.
    /// </summary>
    public interface IStringModel : IModel<string>
    {
    }

    public interface IDistance
    {
        double CosAngle(IVector vec1, IVector vec2);
        double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream);
    }

    public interface IModel<T> : IDistance
    {
        AnalyzedData Tokenize(T data);
        IVector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream);
        long SerializeVector(IVector vector, Stream vectorStream);
        double IdenticalAngle { get; }
        double FoldAngle { get; }
        double PrimaryIndexIdenticalAngle { get; }
        double PrimaryIndexFoldAngle { get; }
        int PageWeight { get; }
        int VectorWidth { get; }
    }
}
