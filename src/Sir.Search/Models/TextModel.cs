using Sir.VectorSpace;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    public class TextModel : DistanceCalculator, ITextModel
    {
        public double IdenticalAngle => 0.88d;
        public double FoldAngle => 0.58d;
        public int UnicodeStartingPoint => 32;
        public override int VectorWidth => 256;

        public void ExecuteFlush(IDictionary<long, VectorNode> columns, Queue<(long keyId, VectorNode node)> unclassified)
        {
        }

        public void ExecutePut<T>(VectorNode column, long keyId, VectorNode node, IModel<T> model, Queue<(long keyId, VectorNode node)> unclassified)
        {
            GraphBuilder.MergeOrAdd(
                column,
                node,
                model);
        }

        public IEnumerable<IVector> Tokenize(string data)
        {
            Memory<char> source = data.ToCharArray();
            var tokens = new List<IVector>();
            
            if (source.Length > 0)
            {
                var embedding = new SortedList<int, float>();
                var offset = 0;
                int index = 0;
                var span = source.Span;

                for (; index < source.Length; index++)
                {
                    char c = char.ToLower(span[index]);

                    if (c < UnicodeStartingPoint || c > UnicodeStartingPoint + VectorWidth)
                    {
                        continue;
                    }

                    if (char.IsLetterOrDigit(c))
                    {
                        embedding.AddOrAppendToComponent(c);
                    }
                    else
                    {
                        if (embedding.Count > 0)
                        {
                            var len = index - offset;

                            var vector = new IndexedVector(
                                embedding,
                                VectorWidth,
                                source.Slice(offset, len).ToString());

                            embedding.Clear();
                            tokens.Add(vector);
                        }

                        offset = index + 1;
                    }
                }

                if (embedding.Count > 0)
                {
                    var len = index - offset;

                    var vector = new IndexedVector(
                                embedding,
                                VectorWidth,
                                source.Slice(offset, len).ToString());

                    tokens.Add(vector);
                }
            }

            return tokens;
        }
    }
}