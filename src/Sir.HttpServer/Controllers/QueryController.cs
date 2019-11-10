using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    [Route("query")]
    public class QueryController : Controller, ILogger
    {
        private readonly PluginsCollection _plugins;
        private readonly IStringModel _model;

        public QueryController(PluginsCollection plugins, IStringModel tokenizer)
        {
            _plugins = plugins;
            _model = tokenizer;
        }

        [HttpGet]
        [HttpPut]
        [HttpPost]
        public IActionResult Get()
        {
            var mediaType = Request.Headers["Accept"].ToArray()[0];
            var reader = _plugins.Get<IHttpReader>(mediaType);

            if (reader == null)
            {
                throw new NotSupportedException(); // Media type not supported
            }

            var timer = Stopwatch.StartNew();
            var result = reader.Read(Request, _model);

            this.Log("processed {0} request in {1}", mediaType, timer.Elapsed);

            Response.Headers.Add("X-Total", result.Total.ToString());

            return new FileContentResult(result.Body, result.MediaType);
        }
    }
}