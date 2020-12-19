namespace Sir.VectorSpace
{
    public interface IIndexingStrategy
    {
        void ExecutePut<T>(VectorNode column, VectorNode node);
    }
}
