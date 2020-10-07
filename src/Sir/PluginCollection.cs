using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir
{
    public class PluginCollection : IDisposable
    {
        private readonly IDictionary<string, IDictionary<Type, IList<IPlugin>>> _services;

        public PluginCollection()
        {
            _services = new Dictionary<string, IDictionary<Type, IList<IPlugin>>>();
        }

        public void Add<T>(string key, T service) where T : IPlugin
        {
            if (!_services.ContainsKey(key))
            {
                _services.Add(key, new Dictionary<Type, IList<IPlugin>>());
            }

            var t = typeof(T);

            if (!_services[key].ContainsKey(t))
            {
                _services[key].Add(t, new List<IPlugin>());
            }

            _services[key][t].Add(service);
        }

        public IDictionary<string, IDictionary<Type, IList<IPlugin>>> ServicesByKey { get { return _services; } }

        public T Get<T>(string key) where T : IPlugin
        {
            return All<T>(key).FirstOrDefault();
        }

        public IEnumerable<T> Get<T>() where T : IPlugin
        {
            foreach (var s in _services.Values.SelectMany(x => x.Values.SelectMany(y => y)))
            {
                if (typeof(T).IsInstanceOfType(s))
                    yield return (T)s;
            }
        }

        public IEnumerable<T> All<T>(string key) where T : IPlugin
        {
            foreach (var s in Services<T>(key))
            {
                yield return (T)s;
            }
        }

        public IEnumerable<T> Services<T>(string key) where T : IPlugin
        {
            if (key == null) key = string.Empty;

            var filter = typeof(T);

            IDictionary<Type, IList<IPlugin>> services;

            if (_services.TryGetValue(key, out services))
            {
                if (filter == typeof(IPlugin))
                {
                    return services.Values.SelectMany(x => x).Cast<T>();
                }

                return services.Values.SelectMany(x => x).Where(x => (x is T)).Cast<T>();
            }
            return Enumerable.Empty<T>();
        }

        public void Dispose()
        {
            foreach(var s in _services.Values.SelectMany(x => x.Values.SelectMany(y => y)))
            {
                s.Dispose();
            }
            _services.Clear();
        }
    }
}
