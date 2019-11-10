using NUnit.Framework;
using System.Linq;

namespace Sir.Search.Tests
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
    }
}