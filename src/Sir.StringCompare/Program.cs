using System;
using Sir.Store;

namespace Sir.StringCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            var tokenizer = new UnicodeTokenizer();

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

                    GraphSerializer.Add(root, new VectorNode(tokenizer.Tokenize(command).Embeddings[0]), Similarity.Term);
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

                    var hit = PathFinder.ClosestMatch(root, tokenizer.Tokenize(command).Embeddings[0], Similarity.Term.foldAngle);

                    Console.WriteLine($"{hit.Score} {hit.Node}");
                }
            }
            else
            {
                var doc1 = new VectorNode(tokenizer.Tokenize(args[0]).Embeddings[0]);
                var doc2 = new VectorNode(tokenizer.Tokenize(args[1]).Embeddings[0]);
                var angle = doc1.Vector.CosAngle(doc2.Vector);

                Console.WriteLine($"similarity: {angle}");
            }
        }
    }
}