using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    [Route("io")]
    public class IOController : Controller
    {
        private PluginsCollection _plugins;

        public IOController(PluginsCollection plugins)
        {
            _plugins = plugins;
        }

        [HttpGet("/io/addpage")]
        public async Task<IActionResult> AddPage(string url)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var str = GetWebString(url.StartsWith("https://") ? url : string.Format("https://{0}", url));
            var decoded = WebUtility.HtmlDecode(str);
            var parser = new YesNoParser('>', '<', new string[] { "script" });
            var body = parser.Parse(decoded);
            var document = new Dictionary<string, object>();

            document["url"] = WebUtility.UrlEncode(url);
            document["body"] = body; 

            var writers = _plugins.All<IWriter>(Request.ContentType ?? string.Empty).ToList();

            if (writers == null || writers.Count == 0)
            {
                return StatusCode(415); // Media type not supported
            }

            foreach (var writer in writers)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        writer.Write("mycol", new[] { document });
                    });
                }
                catch (Exception ew)
                {
                    throw ew;
                }
            }

            return Redirect("/add/thankyou");

            //return StatusCode(201); // Created
        }

        private static string GetWebString(string url)
        {
            var webRequest = WebRequest.Create(url);
            using (var response = webRequest.GetResponse())
            using (var content = response.GetResponseStream())
            using (var reader = new StreamReader(content))
            {
                return reader.ReadToEnd();
            }
        }

        [HttpPost("{*collectionId}")]
        public async Task<IActionResult> Post(string collectionId, [FromBody]IEnumerable<IDictionary> payload)
        {
            if (collectionId == null)
            {
                throw new ArgumentNullException(nameof(collectionId));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(collectionId));
            }

            var writers = _plugins.All<IWriter>(Request.ContentType).ToList();

            if (writers == null || writers.Count == 0)
            {
                return StatusCode(415); // Media type not supported
            }

            foreach (var writer in writers)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        writer.Write(collectionId, payload);
                    });
                }
                catch (Exception ew)
                {
                    throw ew;
                }
            }
            Response.Headers.Add(
                "Location", new Microsoft.Extensions.Primitives.StringValues(string.Format("/io/{0}", collectionId)));

            return StatusCode(201); // Created
        }

        [HttpGet("{*collectionId}")]
        [HttpPut("{*collectionId}")]
        public ObjectResult Get(string collectionId, string query)
        {
            //TODO: add pagination

            var mediaType = Request.ContentType ?? string.Empty;

            if (query == null)
            {
                using (var r = new StreamReader(Request.Body))
                {
                    query = r.ReadToEnd();
                }
            }

            var queryParser = _plugins.Get<IQueryParser>(mediaType);
            var reader = _plugins.Get<IReader>();
            var tokenizer = _plugins.Get<ITokenizer>(mediaType);

            if (queryParser == null || reader == null || tokenizer == null)
            {
                throw new NotSupportedException();
            }

            var parsedQuery = queryParser.Parse(query, tokenizer);
            parsedQuery.CollectionId = collectionId.ToHash();

            var payload = reader.Read(parsedQuery).ToList();

            return new ObjectResult(payload);
        }
    }
}
