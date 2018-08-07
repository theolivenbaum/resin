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

            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data");

            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            Directory.SetCurrentDirectory(dataDir);
        }

        public void Configure(IApplicationBuilder app, IApplicationLifetime applicationLifetime, IHostingEnvironment env)
        {
            applicationLifetime.ApplicationStopping.Register(OnShutdown);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc(routes =>
            {
                routes.MapRoute("default", "{controller}/{action}", new { controller = "Home", action = "Index" });
            });

            app.UseStaticFiles();
        }

        private void OnShutdown()
        {
            foreach (var stopper in ServiceProvider.GetServices<IPluginStop>())
            {
                stopper.OnApplicationShutdown(ServiceProvider);
            }

            var plugins = ServiceProvider.GetService<PluginsCollection>();
            plugins.Dispose();
        }
    }
}
