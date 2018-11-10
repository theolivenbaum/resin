using System.Collections.Generic;
using System.IO;

namespace Sir
{
    public class IniConfiguration : IConfigurationService
    {
        private readonly IDictionary<string, string> _doc;

        public IniConfiguration(string path)
        {
            _doc = new Dictionary<string, string>();

            var file = File.ReadAllText(path).Split('\n');

            foreach (var line in file)
            {
                var segs = line.Split('=');

                _doc.Add(segs[0].Trim(), segs[1].Trim());
            }
        }

        public string Get(string key)
        {
            return _doc[key];
        }
    }
}
