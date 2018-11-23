using Microsoft.Extensions.DependencyInjection;
using System;

namespace Sir
{
    /// <summary>
    /// Implement to register services.
    /// </summary>
    public interface IPluginStart
    {
        void OnApplicationStartup(IServiceCollection services, ServiceProvider serviceProvider);
    }

    /// <summary>
    /// Implement to tear down objects.
    /// </summary>
    public interface IPluginStop
    {
        void OnApplicationShutdown(IServiceProvider serviceProvider);
    }
}
