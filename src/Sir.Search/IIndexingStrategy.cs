using Sir.VectorSpace;
using System.Collections.Generic;

namespace Sir
{
    public interface IIndexingStrategy
    {
        void ExecutePut<T>(VectorNode column, long keyId, VectorNode node, IModel<T> model, Queue<(long keyId, VectorNode node)> unclassified);
        void ExecuteFlush(IDictionary<long, VectorNode> columns, Queue<(long keyId, VectorNode node)> unclassified);
    }
}