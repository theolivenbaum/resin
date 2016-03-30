using System.IO;
using ProtoBuf;

namespace Resin.IO
{
    public class FileBase<T>
    {
        public virtual void Save(string fileName)
        {
            //using (var fs = File.Create(fileName))
            using (var fs = File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var bs = new BufferedStream(fs))
            {
                Serializer.Serialize(bs, this);
            }
        }

        public static T Load(string fileName)
        {
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bs = new BufferedStream(fs))
            {
                return Serializer.Deserialize<T>(bs);
            }
        }
    }
}