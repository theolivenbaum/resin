using System;
using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// Perform calculations on sparse vectors.
    /// </summary>
    public static class VectorOperations
    {
        public static float CosAngle(this SortedList<long, int> vec1, SortedList<long, int> vec2)
        {
            long dotProduct = Dot(vec1, vec2);
            long dotSelf1 = vec1.DotSelf();
            long dotSelf2 = vec2.DotSelf();

            return (float)(dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        public static long Dot(this SortedList<long, int> vec1, SortedList<long, int> vec2)
        {
            if (ReferenceEquals(vec1, vec2))
            {
                return DotSelf(vec1);
            }

            long product = 0;
            var shortest = vec1.Count < vec2.Count ? vec1 : vec2;
            var other = ReferenceEquals(vec1, shortest) ? vec2 : vec1;

            foreach (var component1 in shortest)
            {
                int component2;

                if (other.TryGetValue(component1.Key, out component2))
                {
                    product += (component1.Value * component2);
                }
            }

            return product;
        }

        public static long DotSelf(this SortedList<long, int> vec)
        {
            long product = 0;

            foreach (var component in vec.Values)
            {
                product += (component * component);
            }

            return product;
        }

        public static IVector Add(this IVector vec1, IVector vec2)
        {
            throw new NotImplementedException();
        }

        public static SortedList<long, int> Merge(this SortedList<long, int> vec1, SortedList<long, int> vec2)
        {
            var result = new SortedList<long, int>();

            foreach (var x in vec1)
            {
                result[x.Key] = 1;
            }

            foreach (var x in vec2)
            {
                result[x.Key] = 1;
            }

            return result;
        }

        public static SortedList<long, int> Subtract(this SortedList<long, int> vec1, SortedList<long, int> vec2)
        {
            var result = new SortedList<long, int>();

            foreach (var x in vec1)
            {
                int val;

                if (vec2.TryGetValue(x.Key, out val) && val > 0)
                {
                    result[x.Key] = (val - 1);
                }
            }

            return result;
        }

        public static void AddOrPerformAddition(this SortedList<int, float> vec, int key, float value)
        {
            if (vec.ContainsKey(key))
                vec[key] += value;
            else
                vec.Add(key, value);
        }
        
        public static bool ContainsMany(this string text, char c)
        {
            var found = false;

            foreach(var ch in text.ToCharArray())
            {
                if (c.Equals(ch))
                {
                    if (found)
                        return true;
                    found = true;
                }
            }

            return false;
        }

        public static SortedList<long, int> ToVector(this string word, int offset, int length)
        {
            var vec = new SortedList<long, int>();
            var span = word.AsSpan(offset, length);

            for (int i = 0; i < span.Length; i++)
            {
                var codePoint = (int)span[i];

                if (vec.ContainsKey(codePoint))
                    vec[codePoint] += 1;
                else
                    vec.Add(codePoint, 1);
            }

            return vec;
        }
    }
}
