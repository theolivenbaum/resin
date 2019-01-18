using System;
using System.IO;

namespace Sir
{
    public static class Logging
    {
        private static TextWriter Writer;
        private static object Sync = new object();

        public static bool SendToConsole { get; set; }

        private static void Write(object message)
        {
            var writer = GetWriter();

            writer.WriteLine(message);
            writer.Flush();

            if (SendToConsole)
            {
                Console.WriteLine(message);
            }
        }

        public static void Log(object message)
        {
            Write(string.Format("{0}\t{1}", DateTime.Now, message));
        }

        public static void Log(string format, params object[] args)
        {
            Write(string.Format(DateTime.Now + " " + format, args));
        }

        private static TextWriter GetWriter()
        {
            if (Writer == null)
            {
                lock (Sync)
                {
                    if (Writer == null)
                    {
                        var logDir = Path.Combine(Directory.GetCurrentDirectory(), "log");

                        if (!Directory.Exists(logDir))
                        {
                            Directory.CreateDirectory(logDir);
                        }

                        var fn = Path.Combine(logDir, "sir.log");
                        var stream = Stream.Synchronized(new FileStream(fn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 8*4096));
                        Writer = TextWriter.Synchronized(new StreamWriter(stream));
                        return Writer;
                    }
                }
            }

            return Writer;
        }
    }
}
