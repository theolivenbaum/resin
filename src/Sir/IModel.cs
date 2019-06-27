using System.IO;
using System.IO.MemoryMappedFiles;

namespace Sir
{
    /// <summary>
    /// String vector space model.
    /// </summary>
    public interface IStringModel : IModel<string>
    {
    }

    public interface IModel<T>
    {
        AnalyzedData Tokenize(T data);
        Vector DeserializeVector(long vectorOffset, int componentCount, MemoryMappedViewAccessor vectorView);
        Vector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream);
        long SerializeVector(Vector vector, Stream vectorStream);
        float IdenticalAngle { get; }
        float FoldAngle { get; }
        float CosAngle(Vector vec1, Vector vec2);
    }
}
