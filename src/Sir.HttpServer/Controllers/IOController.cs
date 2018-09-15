using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    [Route("io")]
    public class IOController : Controller
    {
        private readonly PluginsCollection _plugins;
        private readonly StreamWriter _log;

        public IOController(PluginsCollection plugins)
        {
            _plugins = plugins;
            _log = Logging.CreateWriter("iocontroller");
        }

        //[HttpDelete("delete/{*collectionId}")]
        //public async Task<IActionResult> Delete(string collectionId, string q)
        //{
        //    var mediaType = Request.ContentType ?? string.Empty;
        //    var queryParser = _plugins.Get<IQueryParser>(mediaType);
        //    var reader = _plugins.Get<IReader>();
        //    var writers = _plugins.All<IWriter>(mediaType).ToList();
        //    var tokenizer = _plugins.Get<ITokenizer>(mediaType);

        //    if (queryParser == null || writers == null || writers.Count == 0 || tokenizer == null)
        //    {
        //        throw new NotSupportedException();
        //    }

        //    var parsedQuery = queryParser.Parse(q, tokenizer);
        //    parsedQuery.CollectionId = collectionId.ToHash();
        //    var oldData = reader.Read(parsedQuery).ToList();

        //    foreach (var writer in writers)
        //    {
        //        await Task.Run(() =>
        //        {
        //            writer.Remove(collectionId, oldData);
        //        });
        //    }

        //    return StatusCode(202); // marked for deletion
        //}

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
        public ObjectResult Get(string collectionId, string q)
        {
            //TODO: add pagination

            var mediaType = Request.ContentType ?? string.Empty;

            if (q == null)
            {
                using (var r = new StreamReader(Request.Body))
                {
                    q = r.ReadToEnd();
                }
            }

            var queryParser = _plugins.Get<IQueryParser>(mediaType);
            var reader = _plugins.Get<IReader>();
            var tokenizer = _plugins.Get<ITokenizer>(mediaType);

            if (queryParser == null || reader == null || tokenizer == null)
            {
                throw new NotSupportedException();
            }

            var parsedQuery = queryParser.Parse(q, tokenizer);
            parsedQuery.CollectionId = collectionId.ToHash();

            var payload = reader.Read(parsedQuery, int.MaxValue, out _).ToList();

            return new ObjectResult(payload);
        }
    }
}
