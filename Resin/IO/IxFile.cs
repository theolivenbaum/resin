using System.IO;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class IxFile : FileBase<IxFile>
    {
        [ProtoMember(1)]
        private readonly string _fixFileName;
        [ProtoMember(2)]
        private readonly string _dixFileName;

        public IxFile() { }

        public IxFile(string fixFileName, string dixFileName)
        {
            _fixFileName = fixFileName;
            _dixFileName = dixFileName;
        }
        
        public string FixFileName { get{return _fixFileName;} }
        public string DixFileName { get{return _dixFileName;} }

        public override void Save(string fileName)
        {
            base.Save(fileName);
            File.Delete(fileName + ".tmp");
        }
    }
}