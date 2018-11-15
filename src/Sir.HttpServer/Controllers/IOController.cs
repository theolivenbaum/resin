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

        [HttpPost("{*collectionId}")]
        public async Task<HttpResponseMessage> Post(string collectionId)
        {
            if (collectionId == null)
            {
                throw new ArgumentNullException(nameof(collectionId));
            }

            var writer = _plugins.Get<IWriter>(Request.ContentType);

            if (writer == null)
            {
                throw new NotSupportedException(); // Media type not supported
            }

            try
            {
                Result result = await writer.Write(collectionId, Request.Body);

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                Response.Headers.Add("Content-Type", result.MediaType);
                Response.Headers.Add("Content-Length", result.Data.Length.ToString());

                result.Data.CopyTo(Response.Body);

                return response;
            }
            catch (Exception ew)
            {
                throw ew;
            }
        }

        [HttpGet("{*collectionId}")]
        public async Task<HttpResponseMessage> Get(string collectionId)
        {
            var mediaType = Request.Headers["Accept"].ToArray()[0];
            var reader = _plugins.Get<IReader>(mediaType);

            if (reader == null)
            {
                throw new NotSupportedException(); // Media type not supported
            }

            var result = await reader.Read(collectionId, Request);
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            response.Content.Headers.ContentType = new MediaTypeHeaderValue(result.MediaType);
            response.Content.Headers.ContentLength = result.Data.Length;
            response.Content.Headers.Add("x-total", result.Total.ToString());
            response.Content = new StreamContent(result.Data);

            return response;
        }
    }
}