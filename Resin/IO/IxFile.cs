using System;
using System.IO;

namespace Resin.IO
{
    [Serializable]
    public class IxFile : FileBase<IxFile>
    {
        private readonly string _fixFileName;
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