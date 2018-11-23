using System;
using System.Diagnostics;
using System.IO;
using Sir.Core;

namespace Sir
{
    public static class Logging
    {
        private static object Sync = new object();

        private static ProducerConsumerQueue<(StreamWriter w, string s)> _queue = 
            new ProducerConsumerQueue<(StreamWriter w, string s)>(Consume);

        public static bool SendToConsole { get; set; }

        public static bool SendToDebug => true;

        private static void Consume((StreamWriter w, string s) message)
        {
            message.w.WriteLine(message.s);
            message.w.Flush();

            if (SendToConsole)
            {
                Console.WriteLine(message.s);
            }

            if (SendToDebug)
            {
                Debug.WriteLine(message.s);
            }
        }

        public static void Log(this StreamWriter writer, object message)
        {
            _queue.Enqueue((writer, string.Format("{0}\t{1}", DateTime.Now, message)));
        }

        public static void Log(this StreamWriter writer, string format, params object[] args)
        {
            _queue.Enqueue((writer, string.Format(DateTime.Now + " " + format, args)));
        }

        public static StreamWriter CreateWriter(string name)
        {
            lock (Sync)
            {
                var logDir = Path.Combine(Directory.GetCurrentDirectory(), "log");

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var fn = Path.Combine(logDir, string.Format("{0}.log", name));

                return new StreamWriter(File.Open(fn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            }
        }
    }
}
