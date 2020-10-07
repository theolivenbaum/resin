using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    [Produces("application/json")]
    [Route("plugins")]
    public class PluginsController : Controller
    {
        private PluginCollection _plugins;

        public PluginsController(PluginCollection writeActions)
        {
            _plugins = writeActions;
        }

        [HttpGet]
        public IEnumerable<PluginModel> Get()
        {
            return _plugins.ServicesByKey.Select(p => new PluginModel
            {
                Name = p.Key,
                Services = p.Value.Select(s => new PluginServiceModel
                {
                    Name = s.Key.ToString(),
                    Services = s.Value.Select(x => new PluginServiceModel
                    {
                        Name = x.GetType().ToString(),
                        ContentType = x.ContentType
                    })
                })
            });
        }

        public class PluginModel
        {
            public string Name { get; set; }

            public IEnumerable<PluginServiceModel> Services { get; set; }
        }

        public class PluginServiceModel
        {
            public string Name { get; set; }

            public string ContentType { get; set; }

            public IEnumerable<PluginServiceModel> Services { get; set; }
        }
    }

}