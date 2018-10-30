using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace Sir.HttpServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            return new WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(Directory.GetCurrentDirectory())
            //.ConfigureAppConfiguration((builderContext, config) =>
            //{
            //    IHostingEnvironment env = builderContext.HostingEnvironment;

            //    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            //        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            //})
            //.UseUrls("http://localhost:90")
            .UseIISIntegration()
            .UseSetting("detailedErrors", "true")
            .CaptureStartupErrors(true)
            .UseStartup<Startup>()
            .Build();





            //return WebHost.CreateDefaultBuilder(args)
            //.UseStartup<Startup>()
            //.Build();
        }
    }
}
