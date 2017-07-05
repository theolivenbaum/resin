namespace DocumentTable
{
    public interface IReadSessionFactory
    {
        IReadSession OpenReadSession(string docAddressFileName, string docFileName, BatchInfo ix);
    }
}
