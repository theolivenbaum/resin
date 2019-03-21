using System;
using Sir.Store;

namespace Sir.StringCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            var docNode1 = new VectorNode(args[0]);
            var docNode2 = new VectorNode(args[1]);
            var docAngle = docNode1.Vector.CosAngle(docNode2.Vector);

            Console.WriteLine($"similarity: {docAngle}");
            Console.Read();
        }
    }
}
