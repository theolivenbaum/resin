using System;
using System.IO;

namespace Sir
{
    public static class LoggingExtensions
    {
        public static void Log(this StreamWriter writer, string message)
        {
            writer.WriteLine(string.Format("{0} {1}", DateTime.Now, message));
            writer.Flush();
        }
    }


}
