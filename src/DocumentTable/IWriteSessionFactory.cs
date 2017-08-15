namespace DocumentTable
{
    public interface IWriteSessionFactory
    {
        IWriteSession OpenWriteSession(Compression compression);
    }
}