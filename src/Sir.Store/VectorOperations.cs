using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public static class VectorOperations
    {
        public static long Serialize(this SortedList<char, float> vec, Stream stream)
        {
            var pos = stream.Position;

            for (int i = 0; i < vec.Count; i++)
            {
                var c = vec.Keys[i];
                var key = BitConverter.GetBytes(c);
                var val = BitConverter.GetBytes(vec[c]);

                stream.Write(key, 0, key.Length);
                stream.Write(val, 0, val.Length);
            }

            return pos;
        }

        public static float CosAngle(this SortedList<char, float> vec1, SortedList<char, float> vec2)
        {
            var dotProduct = Dot(vec1, vec2);
            var dotSelf1 = Dot(vec1, vec1);
            var dotSelf2 = Dot(vec2, vec2);
            return (float) (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        public static float Dot(this SortedList<char, float> vec1, SortedList<char, float> vec2)
        {
            float product = 0;
            var cursor1 = 0;
            var cursor2 = 0;

            while (cursor1 < vec1.Count && cursor2 < vec2.Count)
            {
                var p1 = vec1.Keys[cursor1];
                var p2 = vec2.Keys[cursor2];

                if (p2 > p1)
                {
                    cursor1++;
                }
                else if (p1 > p2)
                {
                    cursor2++;
                }
                else
                {
                    product += vec1[p1] * vec2[p2];
                    cursor1++;
                    cursor2++;
                }
            }
            return product;
        }

        public static float Dot(this float[] vec1, float[] vec2)
        {
            float product = 0;

            for (int i = 0; i < vec1.Length; i++)
            {
                product += vec1[i] * vec2[i];
            }

            return product;
        }

        public static SortedList<char, float> Add(this SortedList<char, float> vec1, SortedList<char, float> vec2)
        {
            var result = new SortedList<char, float>();

            foreach (var x in vec1)
            {
                float val;
                if (vec2.TryGetValue(x.Key, out val))
                {
                    result[x.Key] = val + x.Value;
                }
                else
                {
                    result[x.Key] = x.Value;
                }
            }

            foreach (var x in vec2)
            {
                if (!vec1.ContainsKey(x.Key))
                {
                    result[x.Key] = x.Value;
                }
            }
            return result;
        }

        public static SortedList<char, int> Subtract(this SortedList<char, int> vec1, SortedList<char, int> vec2)
        {
            var result = new SortedList<char, int>();
            foreach (var x in vec1)
            {
                if (vec2.ContainsKey(x.Key))
                {
                    result[x.Key] = x.Value - vec2[x.Key];
                }
                else
                {
                    result[x.Key] = x.Value;
                }
            }
            foreach (var x in vec2)
            {
                if (!vec1.ContainsKey(x.Key))
                {
                    result[x.Key] = 0 - x.Value;
                }
            }
            return result;
        }

        public static SortedList<char, float> ToVector(this string word)
        {
            if (word.Length == 0) throw new ArgumentException();

            var vec = word.ToCharVector();
            return vec;
        }

        public static string[] ToTriGrams(this string word)
        {
            if (word.Length == 0) throw new ArgumentException();
            if (word.Length < 3) return new string[] { word };

            var result = new string[word.Length - 1];
            for (int i = 1; i < word.Length; i++)
            {
                result[i - 1] = new string(new[] { word[0], word[i] });
            }
            return result;
        }

        public static IEnumerable<string> ToBiGrams(this IEnumerable<string> words)
        {
            string w = null;
            var count = 0;

            foreach (var word in words)
            {
                if (w == null)
                {
                    w = word;
                }
                else
                {
                    yield return string.Join(' ', w, word);
                    w = word;
                }
                count++;
            }

            if (count == 1) yield return w;
        }

        public static SortedList<char, float> ToCharVector(this string word)
        {
            var vec = new SortedList<char, float>();

            for (int i = 0; i < word.Length; i++)
            {
                var c = word[i];
                if (vec.ContainsKey(c))
                {
                    vec[c] += 1;
                }
                else
                {
                    vec[c] = 1;
                }
            }
            return vec;
        }

        public static float Length(this SortedList<char, float> vector)
        {
            return (float) Math.Sqrt(Dot(vector, vector));
        }

        public static ulong Dot(char[] sparse1, char[] sparse2)
        {
            char[] longest, shortest;
            ulong result = 0;

            if (sparse1.Length > sparse2.Length)
            {
                longest = sparse1;
                shortest = sparse2;
            }
            else
            {
                longest = sparse2;
                shortest = sparse1;
            }

            for (int i = 0; i < longest.Length; i++)
            {
                var x = longest[i];
                var y = shortest.Length <= i ? char.MinValue : shortest[i];
                result += (uint)x * y;
            }

            return result;
        }
    }
}
