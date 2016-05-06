using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net;
using NetSerializer;

namespace Resin.IO
{
    [Serializable]
    public abstract class FileBase<T> : FileBase
    {
        public virtual void Save(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException("fileName");
            var timer = new Stopwatch();
            timer.Start();
            if (File.Exists(fileName))
            {
                using (var fs = File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read))
                {
                    Serializer.Serialize(fs, this);
                }
                Log.DebugFormat("re-wrote {0} in {1}", fileName, timer.Elapsed);
            }
            else
            {
                using (var fs = File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Serializer.Serialize(fs, this);
                }
                Log.DebugFormat("created {0} in {1}", fileName, timer.Elapsed);
            }
        }

        public static T Load(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException("fileName");

            var timer = new Stopwatch();
            timer.Start();
            try
            {
                using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var obj = (T)Serializer.Deserialize(fs);
                    Log.DebugFormat("read {0} in {1}", fileName, timer.Elapsed);
                    return obj;
                }
            }
            catch (FileNotFoundException)
            {
                return default(T);
            }
        }
    }

    [Serializable]
    public class FileBase
    {
        [NonSerialized]
        protected static readonly ILog Log = LogManager.GetLogger(typeof(FileBase));

        // to allow conversion between file system versions
        public static readonly int FileSystemVersion = 4;

        [NonSerialized]
        private static readonly Type[] Types =
        {
            typeof (string), 
            typeof (int), 
            typeof (char), 
            typeof (Document),
            typeof (Dictionary<string, string>),
            typeof (Dictionary<string, object>),
            typeof (IxFile),
            typeof (IxInfo),
            typeof (Dictionary<string, Dictionary<string, object>>), 
            typeof (PostingsFile),
            typeof (Dictionary<string, int>)
        };

        [NonSerialized]
        public static readonly Serializer Serializer = new Serializer(Types);
    }
}