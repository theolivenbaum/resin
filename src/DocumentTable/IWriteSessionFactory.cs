namespace DocumentTable
{
    public interface IWriteSessionFactory
    {
        IWriteSession OpenWriteSession();
    }
}