using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net;
using NetSerializer;

namespace Resin.IO
{
    /// <summary>
    /// Base class for the Resin file system.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public abstract class FileBase<T> : FileBase
    {
        public virtual void Save(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException("fileName");
            var timer = new Stopwatch();
            timer.Start();
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
            Log.DebugFormat("saved {0} in {1}", fileName, timer.Elapsed);
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
                    var obj = (T) Serializer.Deserialize(fs);
                    Log.DebugFormat("loaded {0} in {1}", fileName, timer.Elapsed);
                    return obj;
                }
            }
            catch (FileNotFoundException)
            {
                return default(T);
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
            typeof (string), typeof (int), typeof (char), typeof (Trie),
            typeof (Dictionary<string, string>),
            typeof (Dictionary<string, Dictionary<string, object>>), 
            typeof(Dictionary<char, Trie>),
            typeof(Dictionary<string, object>), 
            typeof(DocFieldFile), 
            typeof(DocContainerFile), 
            typeof(PostingsFile), 
            typeof(IxFile)
        };

        public static readonly Serializer Serializer = new Serializer(Types);

        ////Read like Jon Skeet (http://stackoverflow.com/a/221941/46645)
        //public static byte[] ReadFully(Stream input)
        //{
        //    byte[] buffer = new byte[16 * 1024];
        //    using (MemoryStream ms = new MemoryStream())
        //    {
        //        int read;
        //        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        //        {
        //            ms.Write(buffer, 0, read);
        //        }
        //        return ms.ToArray();
        //    }
        //}
    }
}