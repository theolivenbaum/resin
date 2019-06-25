using System;
using Sir.Store;

namespace Sir.StringCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            var model = new BocModel();

            if (args[0] == "-b")
            {
                var root = new VectorNode();

                while (true)
                {
                    Console.WriteLine("enter word:");

                    var command = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(command))
                    {
                        break;
                    }

                    GraphBuilder.Add(root, new VectorNode(model.Tokenize(command).Embeddings[0]), model);
                }

                Console.WriteLine(root.Visualize());

                while (true)
                {
                    Console.WriteLine("enter query:");

                    var command = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(command))
                    {
                        break;
                    }

                    var hit = PathFinder.ClosestMatch(root, model.Tokenize(command).Embeddings[0], model);

                    Console.WriteLine($"{hit.Score} {hit.Node}");
                }
            }
            else
            {
                var doc1 = new VectorNode(model.Tokenize(args[0]).Embeddings[0]);

                var doc2 = new VectorNode(model.Tokenize(args[1]).Embeddings[0]);
                var angle = model.CosAngle(doc1.Vector, doc2.Vector);
                Console.WriteLine($"similarity: {angle}");
            }
        }
    }
}