using System;
using System.IO;
using Sir.Store;

namespace Sir.StringCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            var document1 = args[0];
            var document2 = args[1];

            var docNode1 = new VectorNode(document1);
            var docNode2 = new VectorNode(document2);
            var docAngle = docNode1.Vector.CosAngle(docNode2.Vector);

            if (docAngle >= VectorNode.IdenticalTermAngle)
            {
                Console.Write("{0} is very similar to {1}", document1, document2);
            }
            else
            {
                var tokenizer = new LatinTokenizer();
                var index1 = new VectorNode();
                var tokens = tokenizer.Tokenize(document1);

                foreach (var token in tokens.Tokens)
                {
                    var termVector = tokens.ToCharVector(token.offset, token.length);
                    index1.Add(new VectorNode(termVector), VectorNode.IdenticalTermAngle, VectorNode.TermFoldAngle, new MemoryStream());
                }

                float score = 0;
                var count = 0;
                var tokens2 = tokenizer.Tokenize(document2);
                foreach (var token in tokens2.Tokens)
                {
                    var termVector = tokens.ToCharVector(token.offset, token.length);
                    var node = index1.ClosestMatch(new VectorNode(termVector));

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
