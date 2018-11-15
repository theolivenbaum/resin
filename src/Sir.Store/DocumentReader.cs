using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sir.Store
{
    /// <summary>
    /// Query a document collection.
    /// </summary>
    public class DocumentReader : IReader
    {
        public string ContentType => "application/json";

        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly StreamWriter _log;
        private readonly HttpQueryParser _httpQueryParser;
        private readonly ITokenizer _tokenizer;

        public DocumentReader(LocalStorageSessionFactory sessionFactory, HttpQueryParser httpQueryParser, ITokenizer tokenizer)
        {
            _sessionFactory = sessionFactory;
            _log = Logging.CreateWriter("documentreader");
            _httpQueryParser = httpQueryParser;
            _tokenizer = tokenizer;
        }

        public void Dispose()
        {
            _log.Dispose();
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
                        long total;
                        var docs = session.Read(query, query.Take, out total);

                        _log.Log(string.Format("fetched {0} docs from disk in {1}", docs.Count, timer.Elapsed));

                        timer.Restart();

                        var stream = new MemoryStream();

                        Serialize(docs, stream);

                        _log.Log(string.Format("serialized {0} docs in {1}", docs.Count, timer.Elapsed));

                        return new Result { MediaType = "application/json", Data = stream, Documents = docs, Total = total };
                    }
                }

                return new Result { MediaType = "application/json", Data = new MemoryStream(), Total = 0 };
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("read failed for query: {0} {1}", query.ToString() ?? "unknown", ex));

                throw;
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
