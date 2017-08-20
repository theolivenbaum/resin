using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System;
using Resin.Documents;

namespace Resin.SearchServer
{
    public class Program
    {
        public static IReadSessionFactory SessionFactory { get; private set; }

        public static void Main(string[] args)
        {
            string dataDirectory = "c:\\temp\\resin_data\\pg";
            string postingsServerHostName = null;
            int pport = 0;
            string documentsServerHostName = null;
            int dport = 0;

            if (Array.IndexOf(args, "--ps") > -1)
            {
                postingsServerHostName = args[Array.IndexOf(args, "--ps") + 1];
                pport = int.Parse(args[Array.IndexOf(args, "--pport") + 1]);
            }

            if (Array.IndexOf(args, "--ds") > -1)
            {
                documentsServerHostName = args[Array.IndexOf(args, "--ds") + 1];
                dport = int.Parse(args[Array.IndexOf(args, "--dport") + 1]);
            }

            if (Array.IndexOf(args, "--dir") > -1)
                dataDirectory = args[Array.IndexOf(args, "--dir") + 1];

            var bin = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(dataDirectory);

            if (postingsServerHostName == null && documentsServerHostName == null)
            {
                SessionFactory = new FullTextReadSessionFactory(dataDirectory);
            }
            else
            {
                //TODO: make katamaran servers optional
                SessionFactory = new NetworkFullTextReadSessionFactory(
                        postingsServerHostName, pport, documentsServerHostName, dport, dataDirectory);
            }

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(bin)
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();

            SessionFactory.Dispose();
        }
    }
}
