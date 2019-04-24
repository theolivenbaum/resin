using Microsoft.Extensions.DependencyInjection;

namespace Sir.RocksDb
{
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services, ServiceProvider serviceProvider)
        {
            services.AddSingleton(typeof(IKeyValueStore), 
                new RocksDbStore(serviceProvider.GetService<IConfigurationProvider>()));
        }
    }
}