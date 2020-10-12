using MathNet.Numerics.LinearAlgebra.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Search
{
    public class ImageModel : DistanceCalculator, IImageModel
    {
        public double IdenticalAngle => 0.99d;
        public double FoldAngle => 0.85d;
        public override int VectorWidth => 28;

        public IEnumerable<IVector> Tokenize(IImage data)
        {
            foreach (var row in data.Pixels)
            {
                var vector = new IndexedVector(
                    row.Select(x => Convert.ToSingle(x)), 
                    data.DisplayName);

                if (((SparseVectorStorage<float>)vector.Value.Storage).ValueCount > 0)
                    yield return vector;
            }
        }
    }
}
