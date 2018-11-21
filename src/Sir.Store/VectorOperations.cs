using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    public static class VectorOperations
    {
        public static async Task<long> Serialize(this SortedList<int, byte> vec, Stream stream)
        {
            var pos = stream.Position;

            foreach (var kvp in vec)
            {
                await stream.WriteAsync(BitConverter.GetBytes(kvp.Key), 0, sizeof(int));
                stream.WriteByte(kvp.Value);
            }

            return pos;
        }

        public static float CosAngle(this SortedList<int, byte> vec1, SortedList<int, byte> vec2)
        {
            int dotProduct = Dot(vec1, vec2);
            int dotSelf1 = vec1.DotSelf();
            int dotSelf2 = vec2.DotSelf();

            return (float) (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        public static int Dot(this SortedList<int, byte> vec1, SortedList<int, byte> vec2)
        {
            int product = 0;
            int cursor1 = 0;
            int cursor2 = 0;

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

        public static int DotSelf(this SortedList<int, byte> vec)
        {
            int product = 0;

            foreach (var kvp in vec)
            {
                product += (kvp.Value * kvp.Value);
            }
            return product;
        }

        public static int Dot(this byte[] vec1, byte[] vec2)
        {
            int product = 0;

            for (int i = 0; i < vec1.Length; i++)
            {
                product += vec1[i] * vec2[i];
            }

            return product;
        }

        public static SortedList<int, byte> Add(this SortedList<int, byte> vec1, SortedList<int, byte> vec2)
        {
            var result = new SortedList<int, byte>();

            foreach (var x in vec1)
            {
                byte val;
                if (vec2.TryGetValue(x.Key, out val))
                {
                    int p = Math.Min(byte.MaxValue, (val + x.Value));
                    result[x.Key] = Convert.ToByte(p);
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

        public static SortedList<int, byte> ToVector(this string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException();

           return word.ToCharVector();
        }

        public static SortedList<int, byte> ToCharVector(this string word)
        {
            var vec = new SortedList<int, byte>();
            TextElementEnumerator charEnum = StringInfo.GetTextElementEnumerator(word);

            while (charEnum.MoveNext())
            {
                var element = charEnum.GetTextElement().ToCharArray();
                int codePoint = 0;

                foreach (var c in element)
                {
                    codePoint += c;
                }

                if (vec.ContainsKey(codePoint))
                {
                    vec[codePoint] += 1;
                }
                else
                {
                    vec[codePoint] = 1;
                }
            }
            return vec;
        }

        public static float Length(this SortedList<int, byte> vector)
        {
            return (float) Math.Sqrt(Dot(vector, vector));
        }
    }
}
