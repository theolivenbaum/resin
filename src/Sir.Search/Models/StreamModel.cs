using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Search
{
    public class StreamModel : DistanceCalculator, IStreamModel
    {
        public double IdenticalAngle => 0.9d;
        public double FoldAngle => 0.5d;
        public override int VectorWidth => 28;

        public IEnumerable<IVector> Tokenize(byte[][] data)
        {
            foreach (var row in data)
            {
                yield return new IndexedVector(row.Select(x => Convert.ToSingle(x)), row.Length);
            }
        }
    }
}
