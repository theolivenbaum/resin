using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System;

namespace Resin.DocumentSearchServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configFn = Path.Combine(Directory.GetCurrentDirectory(), "server.config");
            var config = File.ReadAllText(configFn)
                .Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

            string dataDirectory = config[1];

            var bin = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(dataDirectory);

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(bin)
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
