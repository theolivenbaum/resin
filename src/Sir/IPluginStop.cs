using System;

namespace Sir
{
    /// <summary>
    /// Tear down handler.
    /// </summary>
    public interface IPluginStop
    {
        void OnApplicationShutdown(IServiceProvider serviceProvider);
    }
}
