using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sir.Search
{
    /// <summary>
    /// Initialize app.
    /// </summary>
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(
            IServiceCollection services, ServiceProvider serviceProvider, IConfigurationProvider config)
        {
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var model = new TextModel();
            var sessionFactory = new SessionFactory(
                config,
                loggerFactory.CreateLogger<SessionFactory>());

            var qp = new QueryParser<string>(sessionFactory, model, loggerFactory.CreateLogger<QueryParser<string>>());

            var httpParser = new HttpStringQueryParser(qp);

            services.AddSingleton(typeof(ITextModel), model);
            services.AddSingleton(typeof(ISessionFactory), sessionFactory);
            services.AddSingleton(typeof(SessionFactory), sessionFactory);
            services.AddSingleton(typeof(QueryParser<string>), qp);
            services.AddSingleton(typeof(HttpStringQueryParser), httpParser);
            services.AddSingleton(typeof(IHttpWriter), new HttpWriter(sessionFactory));
            services.AddSingleton(typeof(IHttpReader), new HttpReader(
                sessionFactory, 
                httpParser, 
                config, 
                loggerFactory.CreateLogger<HttpReader>()));
        }
    }
}