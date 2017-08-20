namespace Resin.Documents
{
    public interface IDocumentWriteCommand
    {
        void Write(Document document, IWriteSession session);
    }
}