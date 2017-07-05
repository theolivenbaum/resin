using System.IO;

namespace DocumentTable
{
    public interface IWriteSessionFactory
    {
        IWriteSession OpenWriteSession(Stream compoundFile);
    }
}