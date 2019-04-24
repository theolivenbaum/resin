using System;

namespace Sir.RocksDb
{
    public class Stop : IPluginStop
    {
        public void OnApplicationShutdown(IServiceProvider serviceProvider)
        {
            ((IDisposable)serviceProvider.GetService(typeof(IKeyValueStore))).Dispose();
        }
    }
}
