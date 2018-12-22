using Microsoft.Extensions.DependencyInjection;

namespace Sir.Postings
{
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services, ServiceProvider serviceProvider)
        {
            services.AddSingleton(typeof(StreamRepository), 
                new StreamRepository(serviceProvider.GetService<IConfigurationProvider>()));
        }
    }
}