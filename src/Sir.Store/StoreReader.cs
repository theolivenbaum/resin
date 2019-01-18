using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    /// <summary>
    /// Query a collection.
    /// </summary>
    public class StoreReader : IReader, ILogger
    {
        public string ContentType => "application/json";

        private readonly SessionFactory _sessionFactory;
        private readonly HttpQueryParser _httpQueryParser;
        private readonly ITokenizer _tokenizer;

        public StoreReader(SessionFactory sessionFactory, HttpQueryParser httpQueryParser, ITokenizer tokenizer)
        {
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
            _tokenizer = tokenizer;
        }

        public void Dispose()
        {
        }

        public async Task<Result> Read(string collectionId, HttpRequest request)
        {
            Query query = null;

            try
            {
                query = _httpQueryParser.Parse(collectionId, request, _tokenizer);

                ulong keyHash = query.Term.Key.ToString().ToHash();
                long keyId;
                var timer = new Stopwatch();

                timer.Start();

                if (_sessionFactory.TryGetKeyId(keyHash, out keyId))
                {
                    using (var session = _sessionFactory.CreateReadSession(collectionId))
                    {
                        var result = await session.Read(query);
                        var docs = result.Docs;

                        this.Log(string.Format("executed query {0} and read {1} docs from disk in {2}", query, docs.Count, timer.Elapsed));

                        timer.Restart();

                        var stream = new MemoryStream();

                        Serialize(docs, stream);

                        this.Log(string.Format("serialized {0} docs in {1}", docs.Count, timer.Elapsed));

                        return new Result { MediaType = "application/json", Data = stream, Documents = docs, Total = result.Total };
                    }
                }

                return new Result { Total = 0 };
            }
            catch (Exception ex)
            {
                this.Log(string.Format("read failed for query: {0} {1}", query.ToString() ?? "unknown", ex));

                throw;
            }
        }

        private void Serialize(IList<IDictionary> docs, Stream stream)
        {
            var serializer = new DataContractJsonSerializer(typeof(IList<IDictionary>));

            serializer.WriteObject(stream, docs);

            //using (StreamWriter writer = new StreamWriter(stream))
            //using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            //{
            //    JsonSerializer ser = new JsonSerializer();
            //    ser.Serialize(jsonWriter, docs);
            //    jsonWriter.Flush();
            //}
        }

    }
}
