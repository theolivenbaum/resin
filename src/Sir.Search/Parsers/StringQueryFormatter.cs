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
        private readonly SessionFactory _sessionFactory;
        private readonly ILogger _log;

        public StringQueryFormatter(SessionFactory sessionFactory, ILogger log)
        {
            _sessionFactory = sessionFactory;
            _log = log;
        }

        public async Task<string> Format(HttpRequest request, IModel<string> tokenizer)
        {
            var parser = new HttpQueryParser(new QueryParser<string>(_sessionFactory, tokenizer, _log));
            var query = await parser.ParseRequest(request);
            var dictionary = new Dictionary<string, object>();
            
            parser.ParseQuery(query, dictionary);

            return JsonConvert.SerializeObject(dictionary, Formatting.Indented);
        }
    }
}