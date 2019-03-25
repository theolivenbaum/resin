using Sir.Store;
using System.Threading.Tasks;

namespace Sir.HttpServer.Features
{
    public class Conversation
    {
        protected SessionFactory SessionFactory { get; }

        public Conversation(SessionFactory sessionFactory)
        {
            SessionFactory = sessionFactory;
        }

        public virtual async Task<string> Start(string query)
        {
            const string modelName = "chitchat";
            string response = null;
            var formattedQuery = $"body:{query.Replace("\r", "").Replace("\n", "")}";

            var q = new HttpQueryParser(new TermQueryParser(), new LatinTokenizer())
                .FromFormattedString(modelName.ToHash(), formattedQuery);
            q.Take = 1;

            using (var session = SessionFactory.CreateReadSession(modelName, modelName.ToHash()))
            {
                var result = await session.Read(q);

                if (result.Docs.Count > 0)
                {
                    var highscore = (float)result.Docs[0]["___score"] / q.Count();

                    if (highscore > 0.8f)
                    {
                        response = result.Docs[0]["title"].ToString();
                    }
                }
            }

            return response;
        }
    }

    public class D365Conversation : Conversation
    {
        public D365Conversation(SessionFactory sessionFactory) : base(sessionFactory)
        {
        }

        public override async Task<string> Start(string query)
        {
            var baseResult = await base.Start(query);

            if (baseResult == null)
            {
                const string modelName = "www";

                string response = null;
                var cleanQuery = query.Replace("\r", "").Replace("\n", "");
                var formattedQuery = $"body:{cleanQuery} title:{cleanQuery}";

                var q = new HttpQueryParser(new TermQueryParser(), new LatinTokenizer())
                    .FromFormattedString(modelName.ToHash(), formattedQuery);
                q.Take = 3;

                using (var session = SessionFactory.CreateReadSession(modelName, modelName.ToHash()))
                {
                    var result = await session.Read(q);

                    if (result.Docs.Count > 0)
                    {
                        response = "";
                        foreach(var doc in result.Docs)
                        {
                            response += $"{doc["title"]}: {doc["_imageUrl"]} ";
                        }
                    }
                }

                return response;
            }

            return baseResult;
        }
    }
}
