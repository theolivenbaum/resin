namespace Sir.Store
{
    public abstract class CollectionSession 
    {
        protected SessionFactory SessionFactory { get; private set; }
        protected string CollectionName { get; }
        protected ulong CollectionId { get; }

        protected CollectionSession(string collectionName, ulong collectionId, SessionFactory sessionFactory)
        {
            SessionFactory = sessionFactory;
            CollectionId = collectionId;
            CollectionName = collectionName;
        }
    }
}