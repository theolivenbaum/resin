namespace Resin.Documents
{
    public interface IWriteSessionFactory
    {
        IWriteSession OpenWriteSession(Compression compression);
    }
}