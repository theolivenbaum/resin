using System;
using System.Collections.Generic;
using System.IO;

namespace Sir
{
    public class KeyValueConfiguration : IConfigurationProvider
    {
        private IDictionary<string, string> _doc;
        private readonly string _fileName;

        public KeyValueConfiguration(string readFromFileName = null)
        {
            _doc = new Dictionary<string, string>();
            _fileName = Path.Combine(Directory.GetCurrentDirectory(), readFromFileName);

            ReadSettingsFromDisk();
        }

        private void ReadSettingsFromDisk()
        {
            var dic = new Dictionary<string, string>();

            string text;

            using (var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var r = new StreamReader(fs))
            {
                text = r.ReadToEnd();
            }

            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var segs = line.Split('=');

                dic.Add(segs[0].Trim(), segs[1].Trim());
            }

            _doc = dic;
        }

        public string Get(string key)
        {
            string val;

            if (!_doc.TryGetValue(key, out val))
            {
                return null;
            }

            return val;
        }

        public string[] GetMany(string key)
        {
            return Get(key).Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
