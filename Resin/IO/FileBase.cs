using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using NetSerializer;

namespace Resin.IO
{
    [Serializable]
    public class FileBase<T> : FileBase
    {
        public virtual void Save(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException("fileName");
            var dir = Path.GetDirectoryName(fileName) ?? string.Empty;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (File.Exists(fileName))
            {
                using (var fs = File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read))
                {
                    Serializer.Serialize(fs, this);
                }
            }
            else
            {
                using (var fs = File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Serializer.Serialize(fs, this);
                }
            }
        }

        public static T Load(string fileName)
        {
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var obj = (T)Serializer.Deserialize(fs);
                Log.DebugFormat("loaded {0}", fileName);
                return obj;
            }
        }
    }

    public class FileBase
    {
        protected static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // to allow conversion between file system versions
        public static readonly int FileSystemVersion = 2;

        private static readonly Type[] Types =
        {
            typeof (string), typeof (int), typeof (char), typeof (Trie), typeof (Document),
            typeof (Dictionary<string, string>), typeof (Dictionary<string, Document>),
            typeof (Dictionary<string, Dictionary<string, int>>), typeof(Dictionary<char, Trie>),
            typeof(Dictionary<string, object>),
            typeof(DixFile), typeof(DocFile), typeof(FieldFile), typeof(FixFile), typeof(IxFile),
            typeof(Term)
        };

        public static readonly Serializer Serializer = new Serializer(Types);
    }
}