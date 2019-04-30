using Microsoft.Extensions.DependencyInjection;

namespace Sir.Postings
{
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services, ServiceProvider serviceProvider, IConfigurationProvider config)
        {
            var repo = new StreamRepository(config);

            services.AddSingleton(typeof(StreamRepository), repo);
            services.AddSingleton(typeof(IWriter), new PostingsWriter(repo));
            services.AddSingleton(typeof(IReader), new PostingsReader(repo));
        }
    }
}