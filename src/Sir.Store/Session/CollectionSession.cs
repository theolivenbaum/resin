namespace Sir.Store
{
    public abstract class CollectionSession 
    {
        protected SessionFactory SessionFactory { get; private set; }
        protected string Collection { get; }
        protected ulong CollectionId { get; }

        protected CollectionSession(string collectionId, SessionFactory sessionFactory)
        {
            SessionFactory = sessionFactory;
            Collection = collectionId;
            CollectionId = collectionId.ToHash();
        }
    }
}