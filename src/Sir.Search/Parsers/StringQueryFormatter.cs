using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sir.VectorSpace;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sir.Search
{
    public class StringQueryFormatter : IQueryFormatter<string>
    {
        private readonly Database _sessionFactory;
        private readonly ILogger _log;
        private readonly string _directory;

        public StringQueryFormatter(string directory, Database sessionFactory, ILogger log)
        {
            _sessionFactory = sessionFactory;
            _log = log;
            _directory = directory;
        }

        public async Task<string> Format(HttpRequest request, IModel<string> tokenizer)
        {
            var parser = new HttpQueryParser(new QueryParser<string>(_directory, _sessionFactory, tokenizer, _log));
            var query = await parser.ParseRequest(request);
            var dictionary = new Dictionary<string, object>();
            
            parser.ParseQuery(query, dictionary);

            return JsonConvert.SerializeObject(dictionary, Formatting.Indented);
        }
    }
}