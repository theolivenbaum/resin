using System;

namespace Sir.Store
{
    public interface INodeReader : IDisposable
    {
        Hit ClosestMatch(IVector vector, IStringModel model);
    }
}
