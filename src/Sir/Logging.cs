using Sir.Core;
using System;
using System.IO;
using System.Threading;

namespace Sir
{
    public static class Logging
    {
        private static object Sync = new object();

        private static ProducerConsumerQueue<(StreamWriter w, string s)> _queue = 
            new ProducerConsumerQueue<(StreamWriter w, string s)>(Write);

        public static bool SendToConsole { get; set; }

        private static void Write((StreamWriter w, string s) message)
        {
            message.w.WriteLine(message.s);
            message.w.Flush();

            if (SendToConsole)
            {
                Console.WriteLine(message.s);
            }
        }

        public static void Log(this StreamWriter writer, object message)
        {
            _queue.Enqueue((writer, string.Format("{0}\t{1}", DateTime.Now, message)));
        }

        public static void Log(this StreamWriter writer, string format, params object[] args)
        {
            var message = string.Format(DateTime.Now + " " + format, args);

            if (_queue == null || _queue.IsCompleted)
            {
                Write((writer, message));
            }
            else
            {
                _queue.Enqueue((writer, message));
            }
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

                return new StreamWriter(new FileStream(fn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            }
        }

        public static void FlushLog(this StreamWriter log)
        {
            if (_queue != null)
            {
                lock (Sync)
                {
                    if (_queue != null)
                    {
                        Thread.Sleep(1000);

                        _queue.Join();

                        while (!_queue.IsCompleted)
                        {
                            Thread.Sleep(100);
                        }

                        _queue.Dispose();
                        _queue = null;
                    }
                }
            }

            log.Close();
            log.Dispose();
        }
    }
}
