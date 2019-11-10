using System;

namespace Sir.Search
{
    /// <summary>
    /// Teardown app.
    /// </summary>
    public class Stop : IPluginStop
    {
        public void OnApplicationShutdown(IServiceProvider serviceProvider)
        {
            ((SessionFactory)serviceProvider.GetService(typeof(SessionFactory))).Dispose();
        }
    }
}
