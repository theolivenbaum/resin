using System;

namespace Sir.Store
{
    public interface INodeReader : IDisposable
    {
        long KeyId { get; }
        Hit ClosestTerm(IVector vector, IStringModel model);
        Hit ClosestNgram(IVector vector, IStringModel model);
    }
}
