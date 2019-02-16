using Sir.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Sir.Store
{
    /// <summary>
    /// Warm up collection index column caches.
    /// </summary>
    public class WarmupSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ITokenizer _tokenizer;
        private readonly ReadSession _readSession;
        private readonly ProducerConsumerQueue<(long docId, IComparable key, AnalyzedString tokens)> _httpQueue;
        private readonly RemotePostingsReader _postingsReader;
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public WarmupSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config,
            ConcurrentDictionary<long, 
            NodeReader> indexReaders,
            string baseUrl) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _readSession = new ReadSession(CollectionName, CollectionId, SessionFactory, _config, indexReaders);
            _httpQueue = new ProducerConsumerQueue<(long docId, IComparable key, AnalyzedString tokens)>(Validate, 4);
            _postingsReader = new RemotePostingsReader(_config, collectionName);
            _http = new HttpClient();
            _baseUrl = baseUrl;
        }

        public void Warmup(IEnumerable<IDictionary> documents, params long[] excludeKeyIds)
        {
            foreach (var doc in documents)
            {
                var docId = (long)doc["__docid"];

                foreach (var key in doc.Keys)
                {
                    var strKey = key.ToString();

                    if (!strKey.StartsWith("__"))
                    {
                        var keyId = SessionFactory.GetKeyId(strKey.ToHash());

                        if (excludeKeyIds.Contains(keyId))
                        {
                            continue;
                        }

                        var terms = _tokenizer.Tokenize(doc[key].ToString());

                        _httpQueue.Enqueue((docId, (IComparable)key, terms));
                    }       
                }
            }
        }

        private void Validate((long docId, IComparable key, AnalyzedString tokens) item)
        {
            foreach (var token in item.tokens.Tokens.Select(t => item.tokens.Original.Substring(t.offset, t.length)).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var url = string.Format("{0}/Search?q={1}OR=OR&skip=0&take=10&fields=title&fields=body&collection={2}",
                    _baseUrl, token, CollectionName);

                var res = _http.GetAsync(url).Result;

                res.EnsureSuccessStatusCode();
            }

            this.Log("queried doc {0}", item.docId);
        }

        public void Dispose()
        {
            _httpQueue.Dispose();
            _readSession.Dispose();
            _http.Dispose();
        }
    }
}