namespace Sir.Store
{
    public abstract class CollectionSession 
    {
        protected SessionFactory SessionFactory { get; private set; }
        protected ulong CollectionId { get; }

        protected CollectionSession(ulong collectionId, SessionFactory sessionFactory)
        {
            SessionFactory = sessionFactory;
            CollectionId = collectionId;
        }
    }
}