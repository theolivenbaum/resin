using System;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    [Route("write")]
    public class WriteController : Controller, ILogger
    {
        private readonly PluginsCollection _plugins;
        private readonly IStringModel _model;

        public WriteController(PluginsCollection plugins, IStringModel tokenizer)
        {
            _plugins = plugins;
            _model = tokenizer;
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public IActionResult Post()
        {
            if (string.IsNullOrWhiteSpace(Request.ContentType))
            {
                throw new NotSupportedException();
            }

            var writer = _plugins.Get<IHttpWriter>(Request.ContentType);

            if (writer == null)
            {
                writer = _plugins.Get<IHttpWriter>(Request.ContentType.Split(';', StringSplitOptions.RemoveEmptyEntries)[0]);
            }

            if (writer == null)
            {
                throw new NotSupportedException(); // Media type not supported
            }

            try
            {
                writer.Write(Request, _model);

                return Ok();
            }
            catch (Exception ew)
            {
                this.Log(ew);

                throw ew;
            }
        }
    }
}