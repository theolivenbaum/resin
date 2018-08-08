namespace Sir
{
    public interface IRemover : IPlugin
    {
        void Remove(Query query, IReader reader);
    }
}
