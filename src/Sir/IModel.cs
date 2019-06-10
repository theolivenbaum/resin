using System.IO;

namespace Sir
{
    public interface IModel
    {
        AnalyzedString Tokenize(string text);
        Vector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream);
        long SerializeVector(Vector vector, Stream vectorStream);
        (float identicalAngle, float foldAngle) Similarity();
        float CosAngle(Vector vec1, Vector vec2);
    }
}
