﻿using System.Threading.Tasks;

namespace Sir.VectorSpace
{
    public static class Materializer
    {
        /// <summary>
        /// Read document IDs into memory.
        /// </summary>
        public static void Materialize(Query query, IStreamFactory sessionFactory)
        {
            Parallel.ForEach(query.AllTerms(), term =>
            {
                Materialize(term, new PostingsReader(term.Directory, sessionFactory));
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