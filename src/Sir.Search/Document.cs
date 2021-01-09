﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    [JsonConverter(typeof(DocumentJsonConverter))]
    public class Document
    {
        private long _id;

        public long Id { get; set; } 
        public double Score { get; set; }
        public IList<Field> Fields { get; set; }

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

        public Document()
        {
            Fields = new List<Field>();
        }

        public Document(IEnumerable<Field> fields, long documentId = -1, double score = -1)
        {
            _id = documentId;
            Id = documentId;
            Score = score;

            if (fields is IList<Field>)
            {
                Fields = (IList<Field>)fields;
            }
            else
            {
                Fields = new List<Field>();

                foreach (var field in Fields)
                {
                    field.DocumentId = _id;

                    Fields.Add(field);
                }
            }
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

    public class AnalyzedDocument
    {
        public IList<VectorNode> Nodes { get; }

        public AnalyzedDocument(params VectorNode[] nodes)
        {
            Nodes = nodes;
        }

        public AnalyzedDocument(IList<VectorNode> nodes)
        {
            Nodes = nodes;
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

        /// <summary>
        /// https://dotnetfiddle.net/zzlzH4
        /// </summary>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                reader.Read();
                if (reader.TokenType == JsonToken.EndArray)
                    return new Document();
                else
                    throw new JsonSerializationException("Non-empty JSON array does not make a valid Dictionary!");
            }
            else if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            else if (reader.TokenType == JsonToken.StartObject)
            {
                var ret = new Document();
                reader.Read();
                while (reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName)
                        throw new JsonSerializationException("Unexpected token!");
                    string key = (string)reader.Value;
                    reader.Read();
                    if (reader.TokenType != JsonToken.String)
                        throw new JsonSerializationException("Unexpected token!");
                    string value = (string)reader.Value;
                    ret.Fields.Add(new Field(key, value));
                    reader.Read();
                }
                return ret;
            }
            else
            {
                throw new JsonSerializationException("Unexpected token!");
            }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Document);
        }
    }
}