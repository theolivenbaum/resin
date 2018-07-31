using Microsoft.Extensions.DependencyInjection;
using System;

namespace Sir.Store
{
    public class Stop : IPluginStop
    {
        public void OnApplicationShutdown(IServiceProvider serviceProvider)
        {
            var sessionFactory = serviceProvider.GetService<SessionFactory>();
            sessionFactory.Dispose();
        }
    }
}
