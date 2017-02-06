using System.IO;
using System.Linq;
using System.Text;

namespace Resin.IO.Write
{
    public static class LcrsTreeSerializer
    {
        public static void SerializeOld(this LcrsTrie node, string fileName)
        {
            using (var fs = File.Create(fileName))
            using (var sw = new StreamWriter(fs, Encoding.Unicode))
            {
                node.LeftChild.SerializeDepthFirst(sw, 0);
            }
        }

        public static void Serialize(this LcrsTrie node, string fileNameTemplate)
        {
            var ext = Path.GetExtension(fileNameTemplate) ?? "";
            var fileCount = 0;
            var all = node.GetLeftChildAndAllOfItsSiblings().ToList();
            var nodes = all.Count == 1 ? all : all.Fold(all.Count / 2).ToList();

            foreach (var n in nodes)
            {
                var fileName = string.IsNullOrWhiteSpace(ext) ? 
                    fileNameTemplate + "_" + fileCount : 
                    fileNameTemplate.Replace(ext, "_" + fileCount + ext);

                using (var fs = File.Create(fileName))
                using (var sw = new StreamWriter(fs, Encoding.Unicode))
                {
                    n.SerializeDepthFirst(sw, 0);
                }
                fileCount++;

                //n.Balance(fileName);
            }
        }

        private static void Balance(this LcrsTrie node, string fileNameTemplate)
        {
            var fi = new FileInfo(fileNameTemplate);
            var size = fi.Length/1024;

            if (size < 10)
            {
                return;
            }
            
            var ext = Path.GetExtension(fileNameTemplate) ?? "";
            var fileCount = 0;
            var siblings = node.GetAllSiblings().ToList();

            if (siblings.Count > 2)
            {
                var nodes = siblings.Fold(siblings.Count / 2).ToList();

                foreach (var n in nodes)
                {
                    var fileName = string.IsNullOrWhiteSpace(ext) ?
                        fileNameTemplate + "_" + fileCount :
                        fileNameTemplate.Replace(ext, "_" + fileCount + ext);

                    using (var fs = File.Create(fileName))
                    using (var sw = new StreamWriter(fs, Encoding.Unicode))
                    {
                        n.SerializeDepthFirst(sw, 0);
                    }
                    fileCount++;

                    n.Balance(fileName);
                }

                fi.Delete();
            }
        }

        private static void SerializeDepthFirst(this LcrsTrie node, StreamWriter sw, int depth)
        {
            sw.Write(node.Value);
            sw.Write(node.RightSibling == null ? "0" : "1");
            sw.Write(node.LeftChild == null ? "0" : "1");
            sw.Write(node.EndOfWord ? "1" : "0");
            sw.Write(depth);
            sw.Write('\n');

            if (node.LeftChild != null)
            {
                node.LeftChild.SerializeDepthFirst(sw, depth + 1);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.SerializeDepthFirst(sw, depth);
            }
        }
    }
}