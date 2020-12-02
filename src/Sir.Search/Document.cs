using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    [JsonConverter(typeof(DocumentJsonConverter))]
    public class Document
    {
        public long Id { get; set; }
        public double Score { get; set; }
        public IList<Field> Fields { get; }

        public IEnumerable<Field> IndexableFields
        {
            get
            {
                foreach (var field in Fields)
                {
                    if (field.Index && field.Value != null)
                        yield return field;
                }
            }
        }

        public Document(IList<Field> fields, long documentId = -1, double score = -1)
        {
            Fields = fields;
            Id = documentId;
            Score = score;
        }

        public Field Get(string key)
        {
            foreach (var field in Fields)
            {
                if (field.Name == key)
                {
                    return field;
                }
            }

            return null;
        }

        public bool TryGetValue(string key, out Field value)
        {
            foreach (var field in Fields)
            {
                if (field.Name == key)
                {
                    value = field;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }

    public class DocumentJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var document = (Document)value;
            var jo = new JObject();
            
            jo.Add(SystemFields.DocumentId, JToken.FromObject(document.Id));
            jo.Add(SystemFields.Score, JToken.FromObject(document.Score));

            foreach (var field in document.Fields)
            {
                jo.Add(field.Name, JToken.FromObject(field.Value));
            }

            jo.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Document);
        }
    }
}