using System.IO;

namespace Sir
{
    /// <summary>
    /// String vector space model.
    /// </summary>
    /// <example>
    /// [Continuous-bag-of-characters model](https://github.com/kreeben/resin/blob/master/src/Sir.Store/Models/BocModel.cs)
    /// [Lesser-bag-of-words model](https://github.com/kreeben/resin/blob/master/src/Sir.Store/Models/LbocModel.cs)
    /// </example>
    public interface IStringModel : IModel<string>
    {
    }

    public interface IModel<T>
    {
        AnalyzedComputerString Tokenize(T data);
        Vector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream);
        long SerializeVector(Vector vector, Stream vectorStream);
        (float identicalAngle, float foldAngle) Similarity();
        float CosAngle(Vector vec1, Vector vec2);
    }
}
