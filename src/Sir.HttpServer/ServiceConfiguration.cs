using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;

namespace Sir.HttpServer
{
    public static class ServiceConfiguration
    {
        public static IServiceProvider Configure(IServiceCollection services)
        {
            // register config
            services.Add(new ServiceDescriptor(typeof(IConfigurationProvider),
                new IniConfiguration(Path.Combine(Directory.GetCurrentDirectory(), "sir.ini"))));

            // register plugins
            var assemblyPath = Directory.GetCurrentDirectory();

#if DEBUG
            assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), "bin\\Debug\\netcoreapp2.1");
#endif

            var files = Directory.GetFiles(assemblyPath, "*.plugin.dll");

            foreach (var assembly in files.Select(file => AssemblyLoadContext.Default.LoadFromAssemblyPath(file)))
            {
                foreach (var type in assembly.GetTypes())
                {
                    // search for concrete implementations
                    if (!type.IsInterface)
                    {
                        var interfaces = type.GetInterfaces();

                        if (interfaces.Contains(typeof(IPluginStop)) || 
                            interfaces.Contains(typeof(IPluginStart)) ||
                            interfaces.Contains(typeof(IPlugin)))
                        {
                            // register plugins, startup and teardown services
                            foreach(var contract in interfaces.Where(t => t != typeof(IDisposable)))
                            {
                                services.Add(new ServiceDescriptor(
                                    contract, type, ServiceLifetime.Singleton));
                            }
                        }
                    }
                }
            }

            var plugins = new PluginsCollection();

            services.Add(new ServiceDescriptor(typeof(PluginsCollection), plugins));

            var serviceProvider = services.BuildServiceProvider();

            // initiate plugins
            foreach (var service in serviceProvider.GetServices<IPluginStart>())
            {
                service.OnApplicationStartup(services, serviceProvider);
            }

            // Create one instances each of all plugins and register them with the PluginCollection,
            // so that they can be fetched at runtime by Content-Type and System.Type.

            foreach (var service in services.BuildServiceProvider().GetServices<IWriter>())
            {
                plugins.Add(service.ContentType, service);
            }

            foreach (var service in services.BuildServiceProvider().GetServices<IReader>())
            {
                plugins.Add(service.ContentType, service);
            }

            return services.BuildServiceProvider();
        }
    }
}
