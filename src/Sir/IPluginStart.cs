using Microsoft.Extensions.DependencyInjection;
using System;

namespace Sir
{
    public interface IPluginStart
    {
        void OnApplicationStartup(IServiceCollection services);
    }

    public interface IPluginStop
    {
        void OnApplicationShutdown(IServiceProvider serviceProvider);
    }
}
