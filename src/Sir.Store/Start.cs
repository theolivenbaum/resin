﻿using Microsoft.Extensions.DependencyInjection;

namespace Sir.Store
{
    /// <summary>
    /// Initialize app.
    /// </summary>
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(
            IServiceCollection services, ServiceProvider serviceProvider, IConfigurationProvider config)
        {
            var httpParser = new HttpQueryParser(new QueryParser());
            var httpBowParser = new HttpBowQueryParser(httpParser);
            var sessionFactory = new SessionFactory(config);

            services.AddSingleton(typeof(IStringModel), new CbocModel());
            services.AddSingleton(typeof(SessionFactory), sessionFactory);
            services.AddSingleton(typeof(HttpQueryParser), httpParser);
            services.AddSingleton(typeof(HttpBowQueryParser), new HttpBowQueryParser(httpParser));
            services.AddSingleton(typeof(IQueryFormatter), new QueryFormatter());
            services.AddSingleton(typeof(IWriter), new StoreWriter(sessionFactory));
            services.AddSingleton(typeof(IReader), new StoreReader(sessionFactory, httpParser, httpBowParser));
        }
    }
}