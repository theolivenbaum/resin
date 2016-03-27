using System.IO;

namespace Resin
{
    public class Optimizer
    {
        private readonly string _ixfileName;

        public Optimizer(string ixfileName)
        {
            _ixfileName = ixfileName;
        }

        public string Optimize()
        {
            var dir = Path.GetDirectoryName(_ixfileName);
            var optimizedFileName = IndexWriter.ReserveIndexFileName(dir);
            // TODO: optimize
            return optimizedFileName;
        }
    }
}