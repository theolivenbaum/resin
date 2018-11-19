using System;
using System.IO;
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
        public async Task<IActionResult> Post(string collectionId)
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
                Result result = await writer.Write(collectionId, Request);

                return new FileContentResult(result.Data.ToArray(), result.MediaType);
            }
            catch (Exception ew)
            {
                _log.WriteLine(ew);
                throw ew;
            }
        }

        [HttpGet("{*collectionId}")]
        public async Task<IActionResult> Get(string collectionId)
        {
            var mediaType = Request.Headers["Accept"].ToArray()[0];
            var reader = _plugins.Get<IReader>(mediaType);

            if (reader == null)
            {
                throw new NotSupportedException(); // Media type not supported
            }

            try
            {
                var result = await reader.Read(collectionId, Request);

                if (result.Data == null)
                {
                    return new FileContentResult(new byte[0], result.MediaType);
                }
                else
                {
                    Response.Headers.Add("X-Total", result.Total.ToString());

                    return new FileContentResult(result.Data.ToArray(), result.MediaType);
                }
            }
            catch (Exception ew)
            {
                _log.WriteLine(ew);
                throw ew;
            }
}
    }
}