using Microsoft.Extensions.DependencyInjection;
using Sir.HttpServer.Features;
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
            var assemblyPath = Directory.GetCurrentDirectory();

#if DEBUG
            assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), "bin\\Debug\\netcoreapp2.1");
#endif

            var files = Directory.GetFiles(assemblyPath, "*.plugin.dll");

            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "serviceconfig.log"), string.Format("path: {0} files: {1}", assemblyPath, string.Join(",", files)));

            foreach (var assembly in files.Select(file => AssemblyLoadContext.Default.LoadFromAssemblyPath(file)))
            {
                foreach (var type in assembly.GetTypes())
                {
                    // we're looking for concrete implementations
                    if (!type.IsInterface)
                    {
                        var interfaces = type.GetInterfaces();

                        if (interfaces.Contains(typeof(IPluginStop)) || 
                            interfaces.Contains(typeof(IPluginStart)) ||
                            interfaces.Contains(typeof(IPlugin)))
                        {
                            // register plugins and teardown procs
                            foreach(var contract in interfaces.Where(t => t != typeof(IDisposable)))
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

            // initiate plugins
            foreach (var service in serviceProvider.GetServices<IPluginStart>())
            {
                service.OnApplicationStartup(services);
            }

            serviceProvider = services.BuildServiceProvider();

            var plugins = serviceProvider.GetService<PluginsCollection>();

            // Create one instances each of all plugins and register them with the PluginCollection,
            // so that they can be fetched at runtime by Content-Type and System.Type.

            foreach (var service in serviceProvider.GetServices<IHttpQueryParser>())
            {
                plugins.Add(service.ContentType, service);
            }
            foreach (var service in serviceProvider.GetServices<ITokenizer>())
            {
                plugins.Add(service.ContentType, service);
            }

            foreach (var service in serviceProvider.GetServices<IReader>())
            {
                plugins.Add(service.ContentType, service);
            }

            foreach (var service in serviceProvider.GetServices<IWriter>())
            {
                plugins.Add(service.ContentType, service);
            }

            // register crawler as singleton
            services.Add(new ServiceDescriptor(typeof(CrawlQueue), 
                new CrawlQueue(serviceProvider.GetService<PluginsCollection>())));

            return serviceProvider;
        }
    }
}
