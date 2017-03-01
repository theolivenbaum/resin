using System;
using System.Diagnostics;
using System.IO;

namespace Resin.IO
{
    [Serializable]
    public abstract class CompressedBinaryFile<T> : BinaryFile
    {
        public virtual void Save(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException("fileName");

            var timer = new Stopwatch();
            timer.Start();

            using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var memStream = new MemoryStream())
            {
                Serializer.Serialize(memStream, this);

                var bytes = memStream.ToArray();
                var compressed = QuickLZ.compress(bytes, 1);

                fs.Write(compressed, 0, compressed.Length);

                Log.DebugFormat("serialized {0} in {1}", fileName, timer.Elapsed);
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
                using (var memStream = new MemoryStream())
                {
                    fs.CopyTo(memStream);
                    var bytes = memStream.ToArray();
                    var decompressed = QuickLZ.decompress(bytes);
                    var obj = (T)Serializer.Deserialize(new MemoryStream(decompressed));

                    Log.DebugFormat("deserialized {0} in {1}", fileName, timer.Elapsed);

                    return obj;
                }
            }
            catch (FileNotFoundException)
            {
                return default(T);
            }
        }

    }
}