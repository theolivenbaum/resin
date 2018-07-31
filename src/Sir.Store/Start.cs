using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Sir.Store
{
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services)
        {
            services.AddSingleton(typeof(SessionFactory), new SessionFactory(Path.Combine(Directory.GetCurrentDirectory(), "App_Data")));
            services.AddSingleton(typeof(ITokenizer), new Tokenizer());
        }
    }
}
