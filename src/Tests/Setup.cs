using System.Configuration;
using System.IO;
using NUnit.Framework;

namespace Tests
{
    [SetUpFixture]
    public class Setup
    {
        public static string Dir
        {
            get { return ConfigurationManager.AppSettings["resin.datadirectory"]; }
        }

        [SetUp]
        public void RunBeforeAnyTests()
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

            foreach (var dir in Directory.GetDirectories(Dir))
            {
                Directory.Delete(dir, true);
            }            
        }

        [TearDown]
        public void RunAfterAnyTests()
        {
        }
    }
}