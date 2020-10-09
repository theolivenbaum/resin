using System;

namespace Sir.VectorSpace
{
    public interface IColumnReader : IDisposable
    {
        Hit ClosestMatch(IVector vector, IModel model);
    }
}
