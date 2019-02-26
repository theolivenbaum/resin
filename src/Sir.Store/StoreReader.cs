using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

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
        private readonly StoreWriter _storeWriter;
        private readonly ITokenizer _tokenizer;

        public StoreReader(
            SessionFactory sessionFactory, HttpQueryParser httpQueryParser, HttpBowQueryParser httpDocumentQueryParser, ITokenizer tokenizer, IEnumerable<IWriter> storeWriters)
        {
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
            _tokenizer = tokenizer;
            _httpBowQueryParser = httpDocumentQueryParser;
            
            foreach (var writer in storeWriters)
            {
                if (writer is StoreWriter)
                {
                    _storeWriter = (StoreWriter)writer;
                    break;
                }
            }
        }

        public void Dispose()
        {
        }

        public async Task<ResponseModel> Read(string collectionName, HttpRequest request)
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
                    var result = bowReadSession.Read(query, readSession, skip, take);
                    var docs = result.Docs;

                    this.Log(string.Format("executed query {0} and read {1} docs from disk in {2}", query, docs.Count, timer.Elapsed));

                    var stream = new MemoryStream();

                    Serialize(docs, stream);

                    return new ResponseModel { MediaType = "application/json", Stream = stream, Documents = docs, Total = result.Total };
                }
            }
            else
            {
                using (var session = _sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
                {
                    IList<IDictionary> docs;
                    long total;
                    var stream = new MemoryStream();

                    if (request.Query.ContainsKey("id"))
                    {
                        var ids = request.Query["id"].Select(s => long.Parse(s));

                        docs = session.ReadDocs(ids);
                        total = docs.Count;

                        this.Log(string.Format("executed lookup by id in {0}", timer.Elapsed));
                    }
                    else
                    {
                        var query = _httpQueryParser.Parse(collectionName, request);

                        if (query == null)
                        {
                            return new ResponseModel { MediaType = "application/json", Total = 0 };
                        }

                        var result = session.Read(query);

                        docs = result.Docs;
                        total = result.Total;

                        this.Log(string.Format("executed query {0} in {1}", query, timer.Elapsed));

                        if (request.Query.ContainsKey("create"))
                        {
                            var newCollectionName = request.Query["newCollection"].ToString();

                            if (string.IsNullOrWhiteSpace(newCollectionName))
                            {
                                newCollectionName = Guid.NewGuid().ToString();
                            }

                            await _storeWriter.ExecuteWrite(newCollectionName, docs);
                        }
                    }

                    Serialize(docs, stream);

                    return new ResponseModel { MediaType = "application/json", Stream = stream, Documents = docs, Total = total };
                }
            }
        }

        private void Serialize(IList<IDictionary> docs, Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(jsonWriter, docs);
                jsonWriter.Flush();
            }
        }
    }
}