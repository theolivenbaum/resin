using System.IO;
using System.Linq;
using System.Text;

namespace Resin.IO.Write
{
    public static class LcrsTreeSerializer
    {
        public static void Serialize(this LcrsTrie node, string fileName)
        {
            var children = node.GetLeftChildAndAllOfItsSiblings().ToList();

            using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs, Encoding.Unicode))
            using (var w = new LcrsTreeBinaryWriter(sw))
            {
                foreach (var child in children)
                {
                    child.RightSibling = null;
                    w.Write(child);
                }
            }
        }

        //public static void Serialize(this LcrsTrie node, string fileNameTemplate)
        //{
        //    const int folds = 48;
        //    var ext = Path.GetExtension(fileNameTemplate) ?? "";
        //    var fileCount = 0;
        //    var all = node.GetLeftChildAndAllOfItsSiblings().ToList();

        //    var nodes = all.Count == 1 ? 
        //        all : 
        //        all.Fold(Math.Max(2, all.Count / folds)).ToList();

        //    foreach (var n in nodes)
        //    {
        //        var fileName = string.IsNullOrWhiteSpace(ext) ? 
        //            fileNameTemplate + "_" + fileCount : 
        //            fileNameTemplate.Replace(ext, "_" + fileCount + ext);

        //        using (var fs = File.Create(fileName))
        //        using (var sw = new StreamWriter(fs, Encoding.Unicode))
        //        {
        //            n.SerializeDepthFirst(sw, 0);
        //        }
        //        fileCount++;

        //        n.Balance(fileName);
        //    }
        //}

        //private static void Balance(this LcrsTrie node, string fileNameTemplate)
        //{
        //    var fi = new FileInfo(fileNameTemplate);
        //    var size = fi.Length / 1024;

        //    if (size < 100)
        //    {
        //        return;
        //    }

        //    var ext = Path.GetExtension(fileNameTemplate) ?? "";

        //    var siblings = new List<LcrsTrie> { node };
        //    siblings.AddRange(node.GetAllSiblings());

        //    if (siblings.Count > 1)
        //    {
        //        fi.Delete();

        //        var fileCount = 0;

        //        foreach (var n in siblings)
        //        {
        //            var fileName = string.IsNullOrWhiteSpace(ext) ?
        //                fileNameTemplate + "_" + fileCount :
        //                fileNameTemplate.Replace(ext, "_" + fileCount + ext);

        //            using (var fs = File.Create(fileName))
        //            using (var sw = new StreamWriter(fs, Encoding.Unicode))
        //            {
        //                n.RightSibling = null;
        //                n.SerializeDepthFirst(sw, 0);
        //            }

        //            fileCount++;
        //        }
        //    }
        //}

        //private static void SerializeDepthFirst(this LcrsTrie node, StreamWriter sw, int depth)
        //{
        //    sw.Write(node.Value);
        //    sw.Write(node.RightSibling == null ? "0" : "1");
        //    sw.Write(node.LeftChild == null ? "0" : "1");
        //    sw.Write(node.EndOfWord ? "1" : "0");
        //    sw.Write(depth);
        //    sw.Write('\n');

        //    if (node.LeftChild != null)
        //    {
        //        node.LeftChild.SerializeDepthFirst(sw, depth + 1);
        //    }

        //    if (node.RightSibling != null)
        //    {
        //        node.RightSibling.SerializeDepthFirst(sw, depth);
        //    }
        //}

        public static void SerializeDepthFirst(this LcrsTrie node, StringBuilder sb, int depth)
        {
            sb.Append(node.Value);
            sb.Append(node.RightSibling == null ? "0" : "1");
            sb.Append(node.LeftChild == null ? "0" : "1");
            sb.Append(node.EndOfWord ? "1" : "0");
            sb.Append(depth);
            sb.Append('\n');

            if (node.LeftChild != null)
            {
                node.LeftChild.SerializeDepthFirst(sb, depth + 1);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.SerializeDepthFirst(sb, depth);
            }
        }
    }
}