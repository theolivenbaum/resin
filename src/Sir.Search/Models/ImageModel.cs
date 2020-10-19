using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Search
{
    public class ImageModel : DistanceCalculator, IImageModel
    {
        public double IdenticalAngle => 0.95d;
        public double FoldAngle => 0.85d;
        public override int VectorWidth => 784;

        public void ExecuteFlush(IDictionary<long, VectorNode> columns, Queue<(long keyId, VectorNode node)> unclassified)
        {
            //var batchSize = unclassified.Count;
            //var numOfIterations = 0;
            //var lastCount = 0;

            //while (unclassified.Count > 0)
            //{
            //    var queueItem = unclassified.Dequeue();
            //    var column = columns[queueItem.keyId];
            //    VectorNode unclassifiedNode;

            //    if (!GraphBuilder.TryRecalculateVectorOrAdd(column, queueItem.node, this, out unclassifiedNode))
            //    {
            //        unclassified.Enqueue((queueItem.keyId, unclassifiedNode));
            //    }

            //    if (++numOfIterations % batchSize == 0)
            //    {
            //        if (lastCount == unclassified.Count)
            //        {
            //            break;
            //        }
            //        else
            //        {
            //            lastCount = unclassified.Count;
            //        }
            //    }
            //}

            foreach (var queueItem in unclassified)
            {
                var column = columns[queueItem.keyId];
                GraphBuilder.MergeOrAdd(column, queueItem.node, this);
            }
        }

        public void ExecutePut<T>(VectorNode column, long keyId, VectorNode node, IModel<T> model, Queue<(long keyId, VectorNode node)> unclassified)
        {
            VectorNode unclassifiedNode;

            if (!GraphBuilder.TryRecalculateVectorOrAdd(column, node, model, 10, out unclassifiedNode))
            {
                unclassified.Enqueue((keyId, unclassifiedNode));
            }
        }

        public IEnumerable<IVector> Tokenize(IImage data)
        {
            var pixels = data.Pixels.Select(x => Convert.ToSingle(x));

            yield return new IndexedVector(pixels, data.Label);
        }
    }
}