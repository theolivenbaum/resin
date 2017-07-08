namespace DocumentTable
{
    public interface IReadSessionFactory
    {
        IReadSession OpenReadSession(long version);
    }
}
