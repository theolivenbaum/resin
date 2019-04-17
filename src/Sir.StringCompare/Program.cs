using System;
using Sir.Store;

namespace Sir.StringCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            var doc1 = new VectorNode(args[0]);
            var doc2 = new VectorNode(args[1]);
            var angle = doc1.Vector.CosAngle(doc2.Vector);

            Console.WriteLine($"similarity: {angle}");
        }
    }
}
