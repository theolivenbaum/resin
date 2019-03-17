using Sir.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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
        private readonly ProducerConsumerQueue<string> _httpQueue;
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
            _httpQueue = new ProducerConsumerQueue<string>(
                int.Parse(_config.Get("write_thread_count")), callback:SubmitQuery);
            _postingsReader = new RemotePostingsReader(_config, collectionName);
            _http = new HttpClient();
            _baseUrl = baseUrl;

            this.Log("initiated warmup session");
        }

        public void Warmup(IEnumerable<IDictionary> documents, params long[] excludeKeyIds)
        {
            foreach (var doc in documents)
            {
                foreach (var key in doc.Keys)
                {
                    var strKey = key.ToString();

                    if (!strKey.StartsWith("__"))
                    {
                        var keyId = SessionFactory.GetKeyId(CollectionId, strKey.ToHash());

                        if (excludeKeyIds.Contains(keyId))
                        {
                            continue;
                        }

                        var terms = _tokenizer.Tokenize(doc[key].ToString());

                        foreach (var token in terms.Tokens
                            .Select(t => terms.Original.Substring(t.offset, t.length))
                            .Where(s => !string.IsNullOrWhiteSpace(s)))
                        {
                            _httpQueue.Enqueue(token);
                        }
                    }       
                }
            }
        }

        private async Task SubmitQuery(string token)
        {
            try
            {
                var time = Stopwatch.StartNew();

                var url = string.Format("{0}Search?q={1}&OR=OR&skip=0&take=10&fields=title&fields=body&collection={2}",
                                    _baseUrl, token, CollectionName);

                var res = await _http.GetAsync(url);

                res.EnsureSuccessStatusCode();

                this.Log($"{time.Elapsed.TotalMilliseconds} ms len {_httpQueue.Count} {url}");
            }
            catch (Exception ex)
            {
                this.Log(ex);
            }
        }

        public void Dispose()
        {
            this.Log("waiting for warmup session to tear down");

            _httpQueue.Dispose();
            _readSession.Dispose();
            _http.Dispose();
        }
    }
}