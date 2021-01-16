using System.Threading.Tasks;

namespace Sir.VectorSpace
{
    public static class Materializer
    {
        /// <summary>
        /// Read document IDs into memory.
        /// </summary>
        public static void Materialize(Query query, IDatabase sessionFactory)
        {
            Parallel.ForEach(query.AllTerms(), term =>
            {
                using (var reader = new PostingsReader(term.Directory, sessionFactory))
                    Materialize(term, reader);
            });
        }

        public static void Materialize(Term term, PostingsReader postingsReader)
        {
            if (term.PostingsOffsets == null)
                return;

            term.Result = postingsReader.Read(term.CollectionId, term.KeyId, term.PostingsOffsets);
        }
    }
}