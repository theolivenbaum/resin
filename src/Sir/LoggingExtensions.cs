using System;
using System.IO;

namespace Sir
{
    public static class Logging
    {
        public static void Log(this StreamWriter writer, object message)
        {
            writer.WriteLine("{0} {1}", DateTime.Now, message);
            writer.Flush();
        }

        public static void Log(this StreamWriter writer, string format, params object[] args)
        {
            writer.WriteLine(DateTime.Now + " " + format, args);
            writer.Flush();
        }

        public static StreamWriter CreateLogWriter(string name)
        {
            return new StreamWriter(File.Open(name + ".log", FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        }

        static Logging()
        {
            foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.log"))
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }
    }
}
