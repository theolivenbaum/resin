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
            using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Serializer.Serialize(fs, this);
            }
            Log.DebugFormat("created {0} in {1}", fileName, timer.Elapsed);
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
        public static readonly int FileSystemVersion = 5;

        [NonSerialized]
        private static readonly Type[] Types =
        {
            typeof (string), 
            typeof (int), 
            typeof (char), 
            typeof (Dictionary<string, string>),
            typeof (Dictionary<string, object>),
            typeof (DocumentCount),
            typeof (Dictionary<string, Dictionary<string, object>>), 
            typeof (Dictionary<string, int>),
            typeof (Document),
            typeof (IndexInfo),
            typeof (DocumentCount),
            typeof (TermDocumentMatrix),
            typeof (DocumentPosting),
            typeof (Term),
            typeof (Dictionary<Term, List<DocumentPosting>>)
        };

        [NonSerialized]
        public static readonly Serializer Serializer = new Serializer(Types);
    }
}