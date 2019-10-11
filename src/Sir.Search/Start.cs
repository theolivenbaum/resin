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
            var model = new BocModel();
            var sessionFactory = new SessionFactory(config, model);
            var httpParser = new HttpQueryParser(sessionFactory, model);

            services.AddSingleton(typeof(IStringModel), model);
            services.AddSingleton(typeof(ISessionFactory), sessionFactory);
            services.AddSingleton(typeof(SessionFactory), sessionFactory);
            services.AddSingleton(typeof(HttpQueryParser), httpParser);
            services.AddSingleton(typeof(IHttpWriter), new HttpWriter(sessionFactory));
            services.AddSingleton(typeof(IHttpReader), new HttoReader(sessionFactory, httpParser));
        }
    }
}