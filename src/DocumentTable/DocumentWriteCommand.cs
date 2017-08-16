using log4net;

namespace DocumentTable
{
    public class DocumentWriteCommand : IDocumentWriteCommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentWriteCommand));

        public void Write(Document document, IWriteSession session)
        {
            session.Write(document);

            Log.DebugFormat("stored and analyzed doc ID {0}", document.Id);
        }
    }
}