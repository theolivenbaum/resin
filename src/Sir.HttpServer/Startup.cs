using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Sir.HttpServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        public IServiceProvider ServiceProvider { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(options =>
            {
                options.RespectBrowserAcceptHeader = true;
            });
            ServiceProvider = ServiceConfiguration.Configure(services);
            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), "App_Data"));
        }

        public void Configure(IApplicationBuilder app, IApplicationLifetime applicationLifetime, IHostingEnvironment env)
        {
            applicationLifetime.ApplicationStopping.Register(OnShutdown);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }

        private void OnShutdown()
        {
            foreach (var stopper in ServiceProvider.GetServices<IPluginStop>())
            {
                stopper.OnApplicationShutdown(ServiceProvider);
            }

            var plugins = ServiceProvider.GetService<PluginsCollection>();
            plugins.Dispose();

            //foreach(var plugin in ServiceProvider.GetServices<IWriter>())
            //{
            //    plugin.Dispose();
            //}
            //foreach (var plugin in ServiceProvider.GetServices<IReader>())
            //{
            //    plugin.Dispose();
            //}
            //foreach (var plugin in ServiceProvider.GetServices<IModelFormatter>())
            //{
            //    plugin.Dispose();
            //}
            //foreach (var plugin in ServiceProvider.GetServices<IModelBinder>())
            //{
            //    plugin.Dispose();
            //}
            //foreach (var plugin in ServiceProvider.GetServices<IQueryParser>())
            //{
            //    plugin.Dispose();
            //}
        }
    }
}
