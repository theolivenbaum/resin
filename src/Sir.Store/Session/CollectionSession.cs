namespace Sir.Store
{
    public abstract class CollectionSession 
    {
        protected SessionFactory SessionFactory { get; private set; }
        protected string CollectionId { get; }

        protected CollectionSession(string collectionId, SessionFactory sessionFactory)
        {
            SessionFactory = sessionFactory;
            CollectionId = collectionId;
        }
    }
}