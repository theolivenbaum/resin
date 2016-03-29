using System.IO;
using ProtoBuf;

namespace Resin.IO
{
    public class FileBase<T>
    {
        public void Save(string fileName)
        {
            using (var fs = File.Create(fileName))
            {
                Serializer.Serialize(fs, this);
            }
        }

        public static T Load(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                return Serializer.Deserialize<T>(file);
            }
        }
    }
}