using System.Threading.Tasks;

namespace Sir.VectorSpace
{
    public static class Mapper
    {
        /// <summary>
        /// Resolve posting lists and map document IDs to query terms.
        /// </summary>
        public static void Map(Query query, ISessionFactory sessionFactory)
        {
            Parallel.ForEach(query.AllTerms(), term =>
            {
                Map(term, new PostingsReader(sessionFactory));
            });
        }

        public static void Map(Term term, PostingsReader postingsReader)
        {
            if (term.PostingsOffsets == null)
                return;

            term.Result = postingsReader.Read(term.CollectionId, term.KeyId, term.PostingsOffsets);
        }
    }
}