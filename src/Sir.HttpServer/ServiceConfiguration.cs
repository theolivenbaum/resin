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
            // get path to plugins
            var assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), "bin\\Release\\netcoreapp2.0");
#if DEBUG
            assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), "bin\\Debug\\netcoreapp2.0");
#endif

            foreach (var assembly in Directory.GetFiles(assemblyPath, "*.plugin.dll")
                .Select(file=> AssemblyLoadContext.Default.LoadFromAssemblyPath(file)))
            {
                foreach (var type in assembly.GetTypes())
                {
                    // we're looking for concrete implementations
                    if (!type.IsInterface)
                    {
                        var interfaces = type.GetInterfaces();

                        if (interfaces.Contains(typeof(IPluginStart)))
                        {
                            // invoke plugin's startup proc
                            ((IPluginStart)Activator.CreateInstance(type))
                                .OnApplicationStartup(services);
                        }
                        else if (interfaces.Contains(typeof(IPluginStop)) || 
                            interfaces.Contains(typeof(IPlugin)))
                        {
                            // register plugins and teardown procs
                            foreach(var contract in interfaces)
                            {
                                services.Add(new ServiceDescriptor(
                                    contract, type, ServiceLifetime.Singleton));
                            }
                        }
                    }
                }
            }
            services.Add(new ServiceDescriptor(typeof(PluginsCollection), new PluginsCollection()));

            var serviceProvider = services.BuildServiceProvider();
            var plugins = serviceProvider.GetService<PluginsCollection>();

            // Create one instances each of all plugins and register them with the PluginCollection,
            // so that they can be fetched at runtime by Content-Type and System.Type.

            foreach (var service in serviceProvider.GetServices<IWriter>())
            {
                plugins.Add(service.ContentType, service);
            }
            foreach (var service in serviceProvider.GetServices<IReader>())
            {
                plugins.Add(service.ContentType, service);
            }
            foreach (var service in serviceProvider.GetServices<IQueryParser>())
            {
                plugins.Add(service.ContentType, service);
            }

            return serviceProvider;
        }
    }
}
