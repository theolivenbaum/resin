using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Search
{
    /// <summary>
    /// Parse http request query or body into a <see cref="Query"/>.
    /// </summary>
    public class HttpQueryParser
    {
        private readonly QueryParser<string> _parser;

        public HttpQueryParser(QueryParser<string> parser)
        {
            _parser = parser;
        }

        public async Task<Query> ParseRequest(HttpRequest request, IEnumerable<string> collections = null, IEnumerable<string> fields = null)
        {
            string[] select = request.Query["select"].ToArray();

            if (request.Method == "GET")
            {
                if (collections == null)
                    collections = request.Query["collection"].ToArray();

                if (fields == null)
                    fields = request.Query["field"].ToArray();

                var naturalLanguage = request.Query["q"].ToString();
                bool and = request.Query.ContainsKey("AND");
                bool or = !and && request.Query.ContainsKey("OR");

                return _parser.Parse(collections, naturalLanguage, fields.ToArray(), select, and, or);
            }
            else
            {
                var jsonQueryDocument = await DeserializeFromStream(request.Body);

                var query = _parser.Parse(jsonQueryDocument, select);

                return query;
            }
        }

        public static async Task<dynamic> DeserializeFromStream(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var json = await sr.ReadToEndAsync();
                return JsonConvert.DeserializeObject<ExpandoObject>(json);
            }
        }

        public Query ParseFormattedString(string formattedQuery, string[] select)
        {
            var document = JsonConvert.DeserializeObject<IDictionary<string, object>>(
                formattedQuery, new JsonConverter[] { new DictionaryConverter() });

            return ParseDictionary(document, select);
        }

        public Query ParseDictionary(IDictionary<string, object> document, string[] select)
        {
            return _parser.Parse(document, select);
        }

        private void DoParseQuery(Query query, IDictionary<string, object> result)
        {
            if (result == null)
                return;

            var parent = result;
            var q = (Query)query;

            foreach (var term in q.Terms)
            {
                var termdic = new Dictionary<string, object>();

                termdic.Add("collection", term.CollectionId);
                termdic.Add(term.Key, term.Vector.Label);

                if (term.IsIntersection)
                {
                    parent.Add("and", termdic);
                }
                else if (term.IsUnion)
                {
                    parent.Add("or", termdic);
                }
                else
                {
                    parent.Add("not", termdic);
                }

                parent = termdic;
            }

            if (q.And != null)
            {
                ParseQuery(q.And, parent);
            }
            if (q.Or != null)
            {
                ParseQuery(q.Or, parent);
            }
            if (q.Not != null)
            {
                ParseQuery(q.Not, parent);
            }
        }

        public void ParseQuery(Query query, IDictionary<string, object> result)
        {
            DoParseQuery(query, result);
        }
    }

    /// <summary>
    /// https://stackoverflow.com/questions/6416017/json-net-deserializing-nested-dictionaries
    /// </summary>
    public class DictionaryConverter : CustomCreationConverter<IDictionary<string, object>>
    {
        public override IDictionary<string, object> Create(Type objectType)
        {
            return new Dictionary<string, object>();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(object) || base.CanConvert(objectType);
        }

        public override object ReadJson(
            JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject
                || reader.TokenType == JsonToken.Null)
                return base.ReadJson(reader, objectType, existingValue, serializer);

            return serializer.Deserialize(reader);
        }
    }
}
