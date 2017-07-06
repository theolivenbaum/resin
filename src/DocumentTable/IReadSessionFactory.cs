namespace DocumentTable
{
    public interface IReadSessionFactory
    {
        IReadSession OpenReadSession(string docFileName, BatchInfo ix);
    }
}
