using Microsoft.Extensions.DependencyInjection;

namespace Sir.Store
{
    /// <summary>
    /// Initialize app.
    /// </summary>
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services, ServiceProvider serviceProvider, IConfigurationProvider config)
        {
            var tokenizer = new LatinTokenizer();
            var httpParser = new HttpQueryParser(new TermQueryParser(), tokenizer);
            var httpBowParser = new HttpBowQueryParser(tokenizer, httpParser);
            var sessionFactory = new SessionFactory(config.Get("data_dir"), tokenizer, config);
            var writer = new StoreWriter(sessionFactory, tokenizer);

            services.AddSingleton(typeof(ITokenizer), tokenizer);
            services.AddSingleton(typeof(SessionFactory), sessionFactory);
            services.AddSingleton(typeof(HttpQueryParser), httpParser);
            services.AddSingleton(typeof(HttpBowQueryParser), new HttpBowQueryParser(tokenizer, httpParser));
            services.AddSingleton(typeof(IQueryFormatter), new QueryFormatter());
            services.AddSingleton(typeof(IWriter), writer);
            services.AddSingleton(typeof(IReader), new StoreReader(sessionFactory, httpParser, httpBowParser, tokenizer, writer));
        }
    }
}