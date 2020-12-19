using MathNet.Numerics.LinearAlgebra;
using System.IO;

namespace Sir
{
    public interface IVector
    {
        int[] Indices { get; }
        float[] Values { get; }
        Vector<float> Value { get; }
        void Serialize(Stream stream);
        int ComponentCount { get; }
        object Label { get; }
        void AddInPlace(IVector vector);
        IVector Add(IVector vector);
        IVector Subtract(IVector vector);
        void SubtractInPlace(IVector vector);
        IVector Multiply(float scalar);
        IVector Divide(float scalar);
        void AverageInPlace(IVector vector);
        IVector Append(IVector vector);
        IVector Shift(int numOfPositionsToShift, int numOfDimensions, string label = null);
    }
}