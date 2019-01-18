using System.Collections.Generic;
using System.IO;

namespace Sir
{
    public class IniConfiguration : IConfigurationProvider
    {
        private IDictionary<string, string> _doc;
        private readonly string _fileName;

        public IniConfiguration(string fileName)
        {
            _doc = new Dictionary<string, string>();
            _fileName = Path.Combine(Directory.GetCurrentDirectory(), "sir.ini");

            OnFileChanged(null, null);

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Directory.GetCurrentDirectory();
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = "*.ini";
            watcher.Changed += new FileSystemEventHandler(OnFileChanged);
            watcher.EnableRaisingEvents = true;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var dic = new Dictionary<string, string>();
            var file = File.ReadAllText(_fileName).Split('\n');

            foreach (var line in file)
            {
                var segs = line.Split('=');

                dic.Add(segs[0].Trim(), segs[1].Trim());
            }

            _doc = dic;
        }

        public string Get(string key)
        {
            return _doc[key];
        }
    }
}
