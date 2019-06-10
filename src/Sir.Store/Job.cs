using System.Collections.Generic;

namespace Sir.Store
{
    public class Job
    {
        public string Collection { get; private set; }
        public IEnumerable<IDictionary<string, object>> Documents { get; private set; }
        public IStringModel Tokenizer { get; }

        public Job(string collection, IEnumerable<IDictionary<string, object>> documents, IStringModel tokenizer)
        {
            Collection = collection;
            Documents = documents;
            Tokenizer = tokenizer;
        }
    }
}