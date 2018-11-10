using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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

        [HttpDelete("delete/{*collectionId}")]
        public async Task<IActionResult> Delete(string collectionId, string q)
        {
            throw new NotImplementedException();
        }

        [HttpPost("{*collectionId}")]
        public async Task<IActionResult> Post(string collectionId, string id)
        {
            if (collectionId == null)
            {
                throw new ArgumentNullException(nameof(collectionId));
            }

            var writer = _plugins.Get<IWriter>(Request.ContentType);

            if (writer == null)
            {
                return StatusCode(415); // Media type not supported
            }

            long recordId;

            try
            {
                var copy = new MemoryStream();

                await Request.Body.CopyToAsync(copy);

                copy.Position = 0;

                if (id == null)
                {
                    recordId = await writer.Write(collectionId, copy);
                }
                else
                {
                    recordId = long.Parse(id);

                    await writer.Write(collectionId, recordId, copy);
                }
            }
            catch (Exception ew)
            {
                throw ew;
            }

            Response.Headers.Add(
                "Location", new Microsoft.Extensions.Primitives.StringValues(
                    string.Format("{0}/io/{1}?id={2}", Request.Host, collectionId, recordId)));

            return StatusCode(201); // Created
        }

        [HttpGet("{*collectionId}")]
        public async Task<HttpResponseMessage> Get(string collectionId)
        {
            var mediaType = Request.Headers["Accept"].ToArray()[0];
            var reader = _plugins.Get<IReader>(mediaType);

            if (reader == null)
            {
                throw new NotSupportedException();
            }

            var result = await reader.Read(collectionId, Request);
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            response.Content = new StreamContent(result.Data);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(result.MediaType);

            return response;
        }
    }
}