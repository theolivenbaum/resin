using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sir.Store;

namespace Sir.DbUtil
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("processing command: {0}", string.Join(" ", args));

            Logging.SendToConsole = true;

            var command = args[0].ToLower();

            if (command == "query")
            {
                // example: query C:\projects\resin\src\Sir.HttpServer\App_Data www

                Query(
                    dir: args[1], 
                    collectionName: args[2]);
            }
            else if (command == "create-bow")
            {
                // example: create-bow C:\projects\resin\src\Sir.HttpServer\App_Data www 0 1000

                CreateBOWModel(
                    dir: args[1], 
                    collectionName: args[2], 
                    skip: int.Parse(args[3]), 
                    take: int.Parse(args[4]));
            }
            else if (command == "validate")
            {
                // example: validate C:\projects\resin\src\Sir.HttpServer\App_Data www 0 3000

                Validate(
                    dir: args[1], 
                    collectionName: args[2], 
                    skip: int.Parse(args[3]), 
                    take: int.Parse(args[4]));
            }
            else if (command == "warmup")
            {
                // example: warmup C:\projects\resin\src\Sir.HttpServer\App_Data https://didyougogo.com www 0 3000

                var dir = args[1];
                var uri = new Uri(args[2]);
                var collection = args[3];
                var skip = int.Parse(args[4]);
                var take = int.Parse(args[5]);

                Warmup(
                    dir, 
                    uri, 
                    collection, 
                    skip, 
                    take);
            }
            else
            {
                Console.WriteLine("unknown command: {0}", command);
            }

            Console.WriteLine("press any key to exit");
            Console.Read();
        }

        private static void Warmup(string dir, Uri uri, string collectionName, int skip, int take)
        {
            using (var sessionFactory = new SessionFactory(new UnicodeTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                {
                    using (var session = sessionFactory.CreateWarmupSession(collectionName, collectionName.ToHash(), uri.ToString()))
                    {
                        session.Warmup(documentStreamSession.ReadDocs(skip, take), 0);
                    }
                }
            }
        }

        private static void Query(string dir, string collectionName)
        {
            var tokenizer = new UnicodeTokenizer();
            var qp = new TermQueryParser();
            var sessionFactory = new SessionFactory(
                tokenizer,
                new IniConfiguration(Path.Combine(Directory.GetCurrentDirectory(), "sir.ini")));

            while (true)
            {
                Console.Write("query>");

                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input == "q" || input == "quit")
                {
                    break;
                }

                var q = qp.Parse(collectionName.ToHash(), input, tokenizer);
                q.Skip = 0;
                q.Take = 100;

                using (var session = sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
                {
                    var result = session.Read(q);
                    var docs = result.Docs;

                    if (docs.Count > 0)
                    {
                        var index = 0;

                        foreach (var doc in docs.Take(10))
                        {
                            Console.WriteLine("{0} {1} {2}", index++, doc["___score"], doc["title"]);
                        }
                    }
                }
            }
        }
        
        private static void CreateBOWModel(string dir, string collectionName, int skip, int take)
        {
            var files = Directory.GetFiles(dir, "*.docs");
            var time = Stopwatch.StartNew();

            using (var sessionFactory = new SessionFactory(new UnicodeTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                using (var bowSession = sessionFactory.CreateBOWSession(collectionName, collectionName.ToHash()))
                {
                    bowSession.Write(documentStreamSession.ReadDocs(skip, take), 0, 1, 2, 3, 6);
                }
            }

            Logging.Log(null, string.Format("{0} BOW operation took {1}", collectionName, time.Elapsed));
        }

        private static void Validate(string dir, string collectionName, int skip, int take)
        {
            var files = Directory.GetFiles(dir, "*.docs");
            var time = Stopwatch.StartNew();

            using (var sessionFactory = new SessionFactory(new UnicodeTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                using (var validateSession = sessionFactory.CreateValidateSession(collectionName, collectionName.ToHash()))
                {
                    validateSession.Validate(documentStreamSession.ReadDocs(skip, take), 0, 1, 2, 3, 6);
                }
            }

            Logging.Log(null, string.Format("{0} validate operation took {1}", collectionName, time.Elapsed));
        }
    }
}
