using System;
using System.IO;
using System.Threading.Tasks;
using Sir.Store;

namespace Sir.StringCompare
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var document1 = args[0];
            var document2 = args[1];

            var docNode1 = new VectorNode(document1);
            var docNode2 = new VectorNode(document2);
            var docAngle = docNode1.TermVector.CosAngle(docNode2.TermVector);

            if (docAngle >= VectorNode.IdenticalAngle)
            {
                Console.Write("{0} is very similar to {1}", document1, document2);
            }
            else
            {
                var tokenizer = new LatinTokenizer();
                var index1 = new VectorNode();

                foreach (var token in tokenizer.Tokenize(document1))
                {
                    await index1.Add(new VectorNode(token), new MemoryStream());
                }

                float score = 0;
                var count = 0;

                foreach (var token in tokenizer.Tokenize(document2))
                {
                    var node = index1.ClosestMatch(new VectorNode(token));

                    score += node.Score;
                    count++;
                }

                var similarity = (score / count) * 100;

                Console.WriteLine("{0} is {1}% similar to {2}", document1, similarity, document2);
                Console.Read();
            }
        }
    }
}
