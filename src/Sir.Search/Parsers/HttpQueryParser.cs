using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// Parse text from a http request message into a query.
    /// </summary>
    public class HttpQueryParser
    {
        private readonly QueryParser _parser;

        public HttpQueryParser(SessionFactory sessionFactory, IStringModel model)
        {
            _parser = new QueryParser(sessionFactory, model);
        }

        public Query Parse(HttpRequest request)
        {
            var isFormatted = request.Query.ContainsKey("qf");

            if (isFormatted)
            {
                var formattedQuery = request.Query["qf"].ToString();

                return FromFormattedString(formattedQuery);
            }
            else
            {
                if (!request.Query.ContainsKey("collection"))
                {
                    throw new InvalidOperationException("collectionId missing from query string");
                }

                var collectionName = request.Query["collection"].ToString();
                var naturalLanguage = request.Query["q"].ToString();
                string[] fields = request.Query["field"].ToArray();
                bool and = request.Query.ContainsKey("AND");
                bool or = !and && request.Query.ContainsKey("OR");

                return _parser.Parse(collectionName, naturalLanguage, fields, and, or);
            }
        }

        public Query FromFormattedString(string formattedQuery)
        {
            var document = JsonConvert.DeserializeObject<IDictionary<string, object>>(
                formattedQuery, new JsonConverter[] { new DictionaryConverter() });

            return FromDocument(document);
        }

        public Query FromDocument(IDictionary<string, object> document)
        {
            return _parser.ParseQuery(document);
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
