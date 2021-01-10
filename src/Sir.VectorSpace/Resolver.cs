using System.Threading.Tasks;

namespace Sir.VectorSpace
{
    public static class Resolver
    {
        /// <summary>
        /// Resolve posting list locations into document IDs.
        /// </summary>
        public static void Resolve(Query query, IStreamFactory sessionFactory)
        {
            Parallel.ForEach(query.AllTerms(), term =>
            {
                Resolve(term, new PostingsReader(term.Directory, sessionFactory));
            });
        }

        public static void Resolve(Term term, PostingsReader postingsReader)
        {
            if (term.PostingsOffsets == null)
                return;

            term.Result = postingsReader.Read(term.CollectionId, term.KeyId, term.PostingsOffsets);
        }
    }
}