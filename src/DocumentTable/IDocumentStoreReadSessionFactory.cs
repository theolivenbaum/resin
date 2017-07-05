namespace DocumentTable
{
    public interface IDocumentStoreReadSessionFactory
    {
        IDocumentStoreReadSession Create(string docAddressFileName, string docFileName, Compression compression);
    }
}
