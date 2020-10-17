using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sir.Search;

namespace Sir.HttpServer.Controllers
{
    [Route("query")]
    public class QueryController : Controller
    {
        private readonly IHttpReader _reader;
        private readonly ITextModel _model;
        private readonly ILogger<QueryController> _logger;

        public QueryController(IHttpReader reader, ITextModel tokenizer, ILogger<QueryController> logger)
        {
            _reader = reader;
            _model = tokenizer;
            _logger = logger;
        }

        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> Get()
        {
            var mediaType = Request.Headers["Accept"].ToArray()[0];
            var timer = Stopwatch.StartNew();
            var result = await _reader.Read(Request, _model);

            _logger.LogInformation($"processed {mediaType} request in {timer.Elapsed}");

            Response.Headers.Add("X-Total", result.Total.ToString());

            if (result.Total == 0)
                return new EmptyResult();

            return new FileContentResult(result.Body, result.MediaType);
        }
    }
}