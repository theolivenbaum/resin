namespace DocumentTable
{
    public interface IReadSessionFactory
    {
        IReadSession Create(string docAddressFileName, string docFileName, Compression compression);
    }
}
