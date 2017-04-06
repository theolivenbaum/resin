using System;
using System.Configuration;
using System.Globalization;
using System.IO;

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
    }
}