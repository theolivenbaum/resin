using Microsoft.Extensions.DependencyInjection;

namespace Sir.Postings
{
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services)
        {
            services.AddSingleton(typeof(StreamRepository), new StreamRepository());
        }
    }
}
