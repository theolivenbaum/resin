using System;
using Sir.Store;

namespace Sir.StringCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            var angle = new VectorNode(args[0]).TermVector.CosAngle(new VectorNode(args[1]).TermVector);
            Console.WriteLine(angle);
        }
    }
}
