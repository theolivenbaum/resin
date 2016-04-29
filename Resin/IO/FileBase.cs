using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net;
using Lzo64;
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
                    var obj = (T)Serializer.Deserialize(fs);
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

    [Serializable]
    public abstract class CompressedFileBase<T> : FileBase
    {
        public virtual void Save(string fileName, LZOCompressor comp = null)
        {
            if (fileName == null) throw new ArgumentNullException("fileName");

            if (comp == null) comp = new LZOCompressor();
            var timer = new Stopwatch();
            timer.Start();
            if (File.Exists(fileName))
            {
                using (var fs = File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read))
                using (var memStream = new MemoryStream())
                {
                    Serializer.Serialize(memStream, this);
                    var bytes = memStream.ToArray();
                    var compressed = comp.Compress(bytes);
                    fs.Write(compressed, 0, compressed.Length);
                }
            }
            else
            {
                using (var fs = File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var memStream = new MemoryStream())
                {
                    Serializer.Serialize(memStream, this);
                    var bytes = memStream.ToArray();
                    var compressed = comp.Compress(bytes);
                    fs.Write(compressed, 0, compressed.Length);
                }
            }
            Log.DebugFormat("saved {0} in {1}", fileName, timer.Elapsed);
        }

        public static T Load(string fileName, LZOCompressor comp = null)
        {
            if (fileName == null) throw new ArgumentNullException("fileName");

            if(comp == null) comp = new LZOCompressor();
            var timer = new Stopwatch();
            timer.Start();
            try
            {
                using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var memStream = new MemoryStream())
                {
                    fs.CopyTo(memStream);
                    var bytes = memStream.ToArray();
                    var decompressed = comp.Decompress(bytes);
                    var obj = (T)Serializer.Deserialize(new MemoryStream(decompressed));
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
        protected static readonly ILog Log = LogManager.GetLogger(typeof(FileBase));

        // to allow conversion between file system versions
        public static readonly int FileSystemVersion = 3;

        private static readonly Type[] Types =
        {
            typeof (string), 
            typeof (int), 
            typeof (char), 
            typeof (Trie),
            typeof (Dictionary<string, string>),
            typeof (Dictionary<string, Dictionary<string, object>>), 
            typeof (Dictionary<char, Trie>),
            typeof (Dictionary<string, object>), 
            typeof (DocContainerFile), 
            typeof (IxFile),
            typeof (PostingsContainerFile)
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