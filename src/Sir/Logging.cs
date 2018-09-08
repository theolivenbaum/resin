using System;
using System.IO;
using Sir.Core;

namespace Sir
{
    public static class Logging
    {
        private static object _sync = new object();
        private static ProducerConsumerQueue<(StreamWriter w,string s)> _queue = new ProducerConsumerQueue<(StreamWriter w, string s)>(Consume);

        private static void Consume((StreamWriter w, string s) obj)
        {
            obj.w.WriteLine(obj.s);
            obj.w.Flush();
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
            lock (_sync)
            {
                return new StreamWriter(File.Open(name + ".log", FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            }
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
