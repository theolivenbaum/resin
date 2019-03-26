using Sir.Store;
using System.Collections;
using System.Collections.Generic;
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

        public virtual async Task<SortedList<float, IList<IDictionary>>> Evaluate(string formattedQuery)
        {
            const string modelName = "chitchat";
            var documents = new SortedList<float, IList<IDictionary>>();
            var q = new HttpQueryParser(new TermQueryParser(), new LatinTokenizer())
                .FromFormattedString(modelName.ToHash(), formattedQuery);

            using (var session = SessionFactory.CreateReadSession(modelName, modelName.ToHash()))
            {
                var result = await session.Read(q);

                foreach (var document in result.Docs)
                {
                    IList<IDictionary> list;

                    if (!documents.TryGetValue((float)document["___score"], out list))
                    {
                        list = new List<IDictionary>();
                        documents.Add((float)document["___score"], list);
                    }

                    list.Add(document);
                }
            }

            return documents;
        }
    }

    public class D365Conversation : Conversation
    {
        public D365Conversation(SessionFactory sessionFactory) : base(sessionFactory)
        {
        }

        public override async Task<SortedList<float, IList<IDictionary>>> Evaluate(string formattedQuery)
        {
            var documents = await base.Evaluate(formattedQuery);

            const string modelName = "www";

            var q = new HttpQueryParser(new TermQueryParser(), new LatinTokenizer())
                .FromFormattedString(modelName.ToHash(), formattedQuery);
            q.Take = 10;

            using (var session = SessionFactory.CreateReadSession(modelName, modelName.ToHash()))
            {
                var result = await session.Read(q);

                foreach (var document in result.Docs)
                {
                    IList<IDictionary> list;

                    if (!documents.TryGetValue((float)document["___score"], out list))
                    {
                        list = new List<IDictionary>();
                        documents.Add((float)document["___score"], list);
                    }

                    list.Add(document);
                }
            }

            return documents;
        }
    }
}
