using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Sir.Core;
using Sir.HttpServer.Features;
using Sir.Store;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Sir.HttpServer.Controllers
{
    [Route("slack")]
    public class SlackController : Controller, ILogger
    {
        private readonly string _postMessageUrl;
        private static string _token;
        private readonly ProducerConsumerQueue<dynamic> _queue;
        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;

        public SlackController(PluginsCollection plugins, IConfigurationProvider config, SessionFactory sessionFactory)
        {
            _postMessageUrl = "https://slack.com/api/chat.postMessage";
            _queue = new ProducerConsumerQueue<dynamic>(1, callback: Send);
            _sessionFactory = sessionFactory;
            _config = config;
        }

        [HttpPost("challenge")]
        public async Task<IActionResult> Challenge([FromBody]dynamic model)
        {
            string type = model.type;
            string channel = model.channel;

            this.LogJ($"request {model}");

            // TODO: validate request against verification token

            if (type == "event_callback")
            {
                await OnEvent(model.@event);
            }
            else if (type == "url_verification")
            {
                _token = model.token;

                // TODO: store verification token

                return new JsonResult(new { model.challenge });
            }

            return Ok();
        }

        private async Task OnEvent(dynamic eventMessage)
        {
            string type = eventMessage.type;
            string subType = eventMessage.subtype;

            if (subType == null)
            {
                await Act(eventMessage);
            }
        }

        private async Task Act(dynamic eventMessage)
        {
            string query = ((string)eventMessage.text).ToLowerInvariant();
            var cleanQuery = query.Replace("\r", "").Replace("\n", "");
            var formattedQuery = $"body:{cleanQuery} title:{cleanQuery}";
            var dialog = new D365Conversation(_sessionFactory);
            var documents = await dialog.Evaluate(formattedQuery);

            if (documents.Count > 0)
            {
                var highscore = documents[documents.Keys[documents.Count - 1]];
                var response = new StringBuilder();

                foreach (var doc in highscore)
                {
                    var title = doc["title"];
                    var imageUrl = doc.Contains("_imageUrl") ? doc["_imageUrl"] : string.Empty;

                    response.Append($"{title} {imageUrl}  ");
                }

                _queue.Enqueue(new { eventMessage.channel, text = response.ToString() });
            }
        }

        private async Task Send(dynamic eventMessage)
        {
            var time = Stopwatch.StartNew();
            var token = _config.Get("slack_token");
            var url = $"{_postMessageUrl}?token={token}&channel={eventMessage.channel}&text={Uri.EscapeDataString((string)eventMessage.text)}";
            var req = (HttpWebRequest)WebRequest.Create(url);

            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";

            using (var res = (HttpWebResponse)await req.GetResponseAsync())
            {
                this.LogJ($"sent {url}");

                if (res.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"HTTP {res.StatusCode} {res.StatusCode}");
                }

                using (var body = res.GetResponseStream())
                {
                    var responseMessage = Deserialize<dynamic>(body);

                    this.LogJ($"response {responseMessage}");
                }
            }
        }

        private static T Deserialize<T>(Stream s)
        {
            using (StreamReader reader = new StreamReader(s))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer ser = new JsonSerializer();
                return ser.Deserialize<T>(jsonReader);
            }
        }
    }
}
