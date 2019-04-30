using Microsoft.Extensions.DependencyInjection;

namespace Sir
{
    /// <summary>
    /// Plugin bootstrapper
    /// </summary>
    public interface IPluginStart
    {
        void OnApplicationStartup(IServiceCollection services, ServiceProvider serviceProvider, IConfigurationProvider config);
    }
}
