using Sir.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sir
{
    /// <summary>
    /// Perform calculations on sparse vectors.
    /// </summary>
    public static class VectorOperations
    {
        public static float CosAngle(this Vector vec1, Vector vec2)
        {
            long dotProduct = Dot(vec1, vec2);
            long dotSelf1 = vec1.DotSelf();
            long dotSelf2 = vec2.DotSelf();

            return (float) (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        public static long Dot(this Vector vec1, Vector vec2)
        {
            if (ReferenceEquals(vec1, vec2))
                return DotSelf(vec1);

            long product = 0;
            var shorter = vec1.Count < vec2.Count ? vec1 : vec2;
            var longer = ReferenceEquals(vec1, shorter) ? vec2 : vec1;
            int dimension = 0;

            for (;dimension < shorter.Count; dimension++)
            {
                product += shorter.Values.Span[dimension] * longer.Values.Span[dimension];
            }

            return product;
        }

        public static long DotSelf(this Vector vec)
        {
            long product = 0;

            foreach (var component in vec.Values.ToArray())
            {
                product += (component * component);
            }

            return product;
        }

        public static Vector Add(this Vector vec1, Vector vec2)
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

        public static void AddOrAppendToComponent(this SortedList<int, int> vec, int key, int value)
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

        public static int[] ToArray(this SortedList<long, int> vector)
        {
            var result = new int[vector.Count];
            var index = 0;

            foreach(var key in vector.Keys)
            {
                result[index++] = vector[key];
            }

            return result;
        }
    }
}
