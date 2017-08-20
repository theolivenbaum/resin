namespace Resin.Documents
{
    public interface IDocumentWriteCommand
    {
        void Write(DocumentTableRow document, IWriteSession session);
    }
}