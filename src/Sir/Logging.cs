using System;
using System.Diagnostics;
using System.IO;

namespace Sir
{
    public static class Logging
    {
        private static TextWriter Writer;
        private static object Sync = new object();

        public static bool SendToConsole { get; set; }

        private static void Write(this ILogger logger, object message)
        {
            var writer = GetWriter();

            writer.WriteLineAsync(string.Format("{0} {1}", logger?.GetType(), message));
            writer.FlushAsync();

            if (SendToConsole)
            {
                Console.WriteLine(message);
            }

            Debug.WriteLine(message);
        }

        public static void Log(this ILogger logger, object message)
        {
            Write(logger, string.Format("{0}\t{1}", DateTime.Now, message));
        }

        public static void Log(this ILogger logger, string format, params object[] args)
        {
            Write(logger, string.Format(DateTime.Now + " " + format, args));
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
                        var stream = Stream.Synchronized(new FileStream(fn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 8*4096, true));
                        Writer = TextWriter.Synchronized(new StreamWriter(stream));
                        return Writer;
                    }
                }
            }

            return Writer;
        }
    }

    public interface ILogger
    {
    }
}
