using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Sir.Store
{
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services)
        {
            services.AddSingleton(typeof(LocalStorageSessionFactory), new LocalStorageSessionFactory(Path.Combine(Directory.GetCurrentDirectory(), "App_Data")));
            services.AddSingleton(typeof(ITokenizer), new LatinTokenizer());
        }
    }
}
