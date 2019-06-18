using System;
using System.Collections.Generic;
using System.Linq;

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

        public static Vector Add(this Vector vec1, Vector vec2)
        {
            throw new NotImplementedException();
        }

        public static IndexedVector Add(this IndexedVector vec1, IndexedVector vec2)
        {
            var len = Math.Max(vec1.Count, vec2.Count);
            var index = new int[len];
            var values = new int[len];

            var cursor1 = 0;
            var cursor2 = 0;
            var arr1 = vec1.Index.ToArray();
            var arr2 = vec2.Index.ToArray();
            var vals1 = vec1.Values.ToArray();
            var vals2 = vec2.Values.ToArray();

            while (cursor1 < vec1.Count && cursor2 < vec2.Count)
            {
                var i1 = arr1[cursor1];
                var i2 = arr2[cursor2];

                if (i2 > i1)
                {
                    index[cursor1] = arr1[cursor1];
                    values[cursor1] = vals1[cursor1];

                    cursor1++;
                }
                else if (i1 > i2)
                {
                    index[cursor2] = arr2[cursor2];
                    values[cursor2] = vals2[cursor2];

                    cursor2++;
                }
                else
                {
                    index[cursor1] = arr1[cursor1];
                    values[cursor1] = vals1[cursor1++] + vals2[cursor2++];
                }
            }

            return new IndexedVector(index, values);
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

        public static void AddOrPerformAddition(this SortedList<int, int> vec, int key, int value)
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

        public static IndexedVector ToIndexedVector(this string word, int offset, int length)
        {
            var vec = new SortedList<int, int>();
            var span = word.AsSpan(offset, length);

            for (int i = 0; i < span.Length; i++)
            {
                var codePoint = (int)span[i];

                if (vec.ContainsKey(codePoint))
                    vec[codePoint] += 1;
                else
                    vec.Add(codePoint, 1);
            }

            return new IndexedVector(vec.Keys.ToArray().AsMemory(), vec.Values.ToArray().AsMemory());
        }
    }
}
