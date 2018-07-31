using System;

namespace Sir
{
    public interface IPlugin : IDisposable
    {
        string ContentType { get; }
    }
}
