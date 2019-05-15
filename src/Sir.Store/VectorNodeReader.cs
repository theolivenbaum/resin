using System.Collections.Generic;
using System.Text;

namespace Sir.Store
{
    public static class VectorNodeReader
    {
        public static Hit ClosestMatch(VectorNode root, SortedList<long, int> vector, float foldAngle)
        {
            var best = root;
            var cursor = root;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = vector.CosAngle(cursor.Vector);

                if (angle > foldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }

                    cursor = cursor.Left;
                }
                else
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    cursor = cursor.Right;
                }
            }

            return new Hit
            {
                Score = highscore,
                Node = best
            };
        }

        public static Hit FindFirstNonSimilar(VectorNode root, SortedList<long, int> vector, float foldAngle)
        {
            var cursor = root;

            while (cursor != null)
            {
                var angle = vector.CosAngle(cursor.Vector);

                if (angle < foldAngle)
                {
                    return new Hit
                    {
                        Score = angle,
                        Node = cursor
                    };

                }
                else if (cursor.Right != null)
                {
                    cursor = cursor.Right;
                }
                else
                {
                    cursor = cursor.Left;
                }
            }

            return new Hit();
        }

        public static IEnumerable<VectorNode> All(VectorNode root)
        {
            var node = root;
            var stack = new Stack<VectorNode>();

            while (node != null)
            {
                yield return node;

                if (node.Right != null)
                {
                    stack.Push(node.Right);
                }

                node = node.Left;

                if (node == null)
                {
                    if (stack.Count > 0)
                        node = stack.Pop();
                }
            }
        }

        public static SortedList<long, int> Compress(VectorNode root)
        {
            var vector = new SortedList<long, int>();

            foreach (var node in All(root))
            {
                vector = VectorOperations.Merge(vector, node.Vector);
            }

            return vector;
        }

        public static string Visualize(VectorNode root)
        {
            StringBuilder output = new StringBuilder();
            Visualize(root, output, 0);
            return output.ToString();
        }

        private static void Visualize(VectorNode node, StringBuilder output, int depth)
        {
            if (node == null) return;

            output.Append('\t', depth);
            output.AppendFormat($"{node.AngleWhenAdded} {node} w:{node.Weight}");
            output.AppendLine();

            Visualize(node.Left, output, depth + 1);
            Visualize(node.Right, output, depth);
        }

        public static (int depth, int width, int avgDepth) Size(VectorNode root)
        {
            var width = 0;
            var depth = 1;
            var node = root;
            var aggDepth = 0;
            var count = 0;

            while (node != null)
            {
                var d = Depth(node);
                if (d > depth)
                {
                    depth = d;
                }

                aggDepth += d;
                count++;
                width++;

                node = node.Right;
            }

            return (depth, width, aggDepth / count);
        }

        public static int Depth(VectorNode node)
        {
            var count = 0;

            while (node != null)
            {
                count++;
                node = node.Left;
            }
            return count;
        }
    }
}
