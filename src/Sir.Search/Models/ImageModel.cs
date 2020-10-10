using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Search
{
    public class ImageModel : DistanceCalculator, IImageModel
    {
        public double IdenticalAngle => 0.95d;
        public double FoldAngle => 0.85d;
        public override int VectorWidth => 28;

        private readonly IVector _emptyVector;

        public ImageModel()
        {
            _emptyVector = new IndexedVector(VectorWidth);
        }

        public IEnumerable<IVector> Tokenize(byte[][] data)
        {
            foreach (var row in data)
            {
                var vector = new IndexedVector(row.Select(x => Convert.ToSingle(x)), row.Length);

                if (CosAngle(vector, _emptyVector) < 1)
                    yield return vector;
            }
        }
    }
}
