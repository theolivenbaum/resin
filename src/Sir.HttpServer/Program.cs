using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// http://didyouwhysir.com/
/// </summary>
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
            //return new WebHostBuilder()
            //.UseKestrel()
            //.UseContentRoot(Directory.GetCurrentDirectory())
            //.ConfigureAppConfiguration((builderContext, config) =>
            //{
            //    IHostingEnvironment env = builderContext.HostingEnvironment;

            //    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            //        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            //})
            //.UseIISIntegration()
            //.UseStartup<Startup>()
            //.Build();





            return WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .Build();
        }
    }
}
