using System;
using System.IO;

namespace Sir
{
    public static class Logging
    {
        public static void Log(this StreamWriter writer, string message)
        {
            writer.WriteLine(string.Format("{0} {1}", DateTime.Now, message));
            writer.Flush();
        }

        public static StreamWriter CreateLogWriter(string name)
        {
            return new StreamWriter(File.Open(name + ".log", FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        }
    }


}
