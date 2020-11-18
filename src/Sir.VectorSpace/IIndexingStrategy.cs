namespace Sir.VectorSpace
{
    public interface IIndexingStrategy
    {
        void ExecutePut<T>(VectorNode column, long keyId, VectorNode node);
    }
}
