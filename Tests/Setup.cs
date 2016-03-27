using System.IO;
using NUnit.Framework;

namespace Tests
{
    [SetUpFixture]
    public class Setup
    {
        public const string Dir = @"c:\temp\resin_tests";
        [SetUp]
        public void RunBeforeAnyTests()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        }

        [TearDown]
        public void RunAfterAnyTests()
        {
        }
    }
}