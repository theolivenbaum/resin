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
        private readonly HttpBowQueryParser _httpBowQueryParser;
        private readonly ITokenizer _tokenizer;

        public StoreReader(
            SessionFactory sessionFactory, HttpQueryParser httpQueryParser, HttpBowQueryParser httpDocumentQueryParser, ITokenizer tokenizer)
        {
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
            _tokenizer = tokenizer;
            _httpBowQueryParser = httpDocumentQueryParser;
        }

        public void Dispose()
        {
        }

        public async Task<ResultModel> Read(string collectionName, HttpRequest request)
        {
            try
            {
                var timer = Stopwatch.StartNew();

                var vec1FileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.vec1", collectionName.ToHash()));

                if (File.Exists(vec1FileName))
                {
                    using (var readSession = _sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
                    using (var bowReadSession = _sessionFactory.CreateBOWReadSession(collectionName, collectionName.ToHash()))
                    {
                        int skip = 0;
                        int take = 10;

                        if (request.Query.ContainsKey("take"))
                            take = int.Parse(request.Query["take"]);

                        if (request.Query.ContainsKey("skip"))
                            skip = int.Parse(request.Query["skip"]);

                        var query = _httpBowQueryParser.Parse(collectionName, request, readSession, _sessionFactory);
                        var result = await bowReadSession.Read(query, readSession, skip, take);
                        var docs = result.Docs;

                        this.Log(string.Format("executed query {0} and read {1} docs from disk in {2}", query, docs.Count, timer.Elapsed));

                        var stream = new MemoryStream();

                        Serialize(docs, stream);

                        return new ResultModel { MediaType = "application/json", Data = stream, Documents = docs, Total = result.Total };
                    }
                }
                else
                {
                    using (var session = _sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
                    {
                        var query = _httpQueryParser.Parse(collectionName, request);
                        var result = await session.Read(query);
                        var docs = result.Docs;

                        this.Log(string.Format("executed query {0} and read {1} docs from disk in {2}", query, docs.Count, timer.Elapsed));

                        var stream = new MemoryStream();

                        Serialize(docs, stream);

                        return new ResultModel { MediaType = "application/json", Data = stream, Documents = docs, Total = result.Total };
                    }
                }
            }
            catch (Exception ex)
            {
                this.Log(ex);

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
