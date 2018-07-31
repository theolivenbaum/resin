using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir
{
    public class PluginsCollection : IDisposable
    {
        private readonly IDictionary<string, IDictionary<Type, IList<IPlugin>>> _services;

        public PluginsCollection()
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
            if(!_services[key].ContainsKey(t))
            {
                _services[key].Add(t, new List<IPlugin>());
            }
            var list = _services[key][t];
            list.Add(service);
        }

        public IEnumerable<string> Keys { get { return _services.Keys; } }

        public T Get<T>(string key) where T : IPlugin
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return All<T>(key).FirstOrDefault();
        }

        public T Get<T>() where T : IPlugin
        {
            return Services<T>("*").FirstOrDefault();
        }

        public IEnumerable<T> All<T>(string key, bool includeWildcardServices = true) where T : IPlugin
        {
            if (includeWildcardServices)
            {
                foreach (var s in Services<T>("*"))
                {
                    yield return (T)s;
                }
            }
            
            foreach (var s in Services<T>(key))
            {
                yield return (T)s;
            }
        }

        private IEnumerable<T> Services<T>(string key) where T : IPlugin
        {
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
