using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Resin.Sys
{
    public static class Util
    {
        public static readonly DateTime BeginningOfTime = new DateTime(2016, 4, 23);

        public static string GetChronologicalFileId()
        {
            var ticks = DateTime.Now.Ticks - BeginningOfTime.Ticks;
            return ticks.ToString(CultureInfo.InvariantCulture);
        }

        public static string GetDataDirectory()
        {
            var configPath = ConfigurationManager.AppSettings.Get("resin.datadirectory");
            if (!string.IsNullOrWhiteSpace(configPath)) return configPath;
            string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Directory.GetParent(path).ToString();
            }
            return Path.Combine(path, "Resin");
        }

        private static IEnumerable<string> GetIndexFileNames(string directory)
        {
            return Directory.GetFiles(directory, "*.ix");
        }

        public static IEnumerable<string> GetIndexFileNamesInChronologicalOrder(string directory)
        {
            return GetIndexFileNames(directory)
                .Select(f => new {id = long.Parse(new FileInfo(f).Name.Replace(".ix", "")), fileName = f})
                .OrderBy(info => info.id)
                .Select(info => info.fileName);
        }
    }
}