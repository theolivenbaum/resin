using System;
using log4net;
using log4net.Config;
using Nancy;
using Nancy.Hosting.Self;
using Resin;

namespace Host
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SearchClient));

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            Run(args);
        }

        static void Run(string[] args)
        {
            var url = Array.IndexOf(args, "--url") == -1 ? "http://localhost:1234/" : args[Array.IndexOf(args, "--url") + 1];
            var input = string.Empty;
            var config = new HostConfiguration {UrlReservations = {CreateAutomatically = true}};
            using (var host = new NancyHost(new Uri(url), new DefaultNancyBootstrapper(), config))
            {
                host.Start();
                Log.InfoFormat("server started on {0}", url);
                while (input != "stop" && input != "restart")
                {
                    input = Console.ReadLine();
                }
            }
            Log.Info("server stopped");
            if (input == "restart") Run(args);
        }
    }
}
