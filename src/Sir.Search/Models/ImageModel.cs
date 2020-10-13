using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Search
{
    public class ImageModel : DistanceCalculator, IImageModel
    {
        public double IdenticalAngle => 0.9d;
        public double FoldAngle => 0.6d;
        public override int VectorWidth => 784;

        public IEnumerable<IVector> Tokenize(IImage data)
        {
            var vector = new IndexedVector(
                    data.Pixels.Select(x => Convert.ToSingle(x)),
                    data.DisplayName);

            yield return vector;
        }
    }
}
