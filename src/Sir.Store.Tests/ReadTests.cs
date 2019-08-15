using NUnit.Framework;
using System.Linq;

namespace Sir.Store.Tests
{
    public class ReadTests
    {
        private SessionFactory _sessionFactory;

        [SetUp]
        public void Setup()
        {
            _sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), new BocModel());
        }

        [TearDown]
        public void TearDown()
        {
            _sessionFactory.Dispose();
        }

        [Test]
        public void Can_validate()
        {
            var documents = new GoogleFeed("google-feed.json").Take(100);
            var collection = "Can_validate";
            var collectionId = collection.ToHash();
            var comparer = new DocumentComparer();
            var model = new BocModel();

            _sessionFactory.Truncate(collectionId);

            _sessionFactory.Write(new Job(collectionId, documents, model));

            using (var documentStreamSession = _sessionFactory.CreateDocumentStreamSession(collectionId))
            using (var validateSession = _sessionFactory.CreateValidateSession(collectionId))
            {
                validateSession.Validate(documentStreamSession.ReadDocs());
            }
        }
    }
}