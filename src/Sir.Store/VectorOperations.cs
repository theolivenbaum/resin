using Sir.Store;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Sir
{
    /// <summary>
    /// Perform calculations on sparse vectors.
    /// </summary>
    public static class VectorOperations
    {
        public static async Task<long> SerializeAsync(this SortedList<long, byte> vec, Stream stream)
        {
            var pos = stream.Position;

            foreach (var kvp in vec)
            {
                await stream.WriteAsync(BitConverter.GetBytes(kvp.Key), 0, sizeof(long));
                await stream.WriteAsync(new[] { kvp.Value }, 0, sizeof(byte));
            }

            return pos;
        }

        public static long Serialize(this SortedList<long, byte> vec, Stream stream)
        {
            lock (stream)
            {
                var pos = stream.Position;

                foreach (var kvp in vec)
                {
                    stream.Write(BitConverter.GetBytes(kvp.Key), 0, sizeof(long));
                    stream.WriteByte(kvp.Value);
                }

                return pos;
            }
        }

        public static float CosAngle(this SortedList<long, byte> vec1, SortedList<long, byte> vec2)
        {
            long dotProduct = Dot(vec1, vec2);
            long dotSelf1 = vec1.DotSelf();
            long dotSelf2 = vec2.DotSelf();

            return (float) (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        public static long Dot(this SortedList<long, byte> vec1, SortedList<long, byte> vec2)
        {
            long product = 0;
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

        public static long DotSelf(this SortedList<long, byte> vec)
        {
            long product = 0;

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

        public static SortedList<long, byte> Add(this SortedList<long, byte> vec1, SortedList<long, byte> vec2)
        {
            var result = new SortedList<long, byte>();

            foreach (var x in vec1)
            {
                byte val;

                if (vec2.TryGetValue(x.Key, out val) && val < byte.MaxValue)
                {
                    var v = (byte)(val + x.Value);

                    result[x.Key] = v;
                }
                else
                {
                    result[x.Key] = x.Value;
                }
            }

            foreach (var x in vec2)
            {
                byte val;

                if (vec1.TryGetValue(x.Key, out val) && val < byte.MaxValue)
                {
                    var v = (byte)(val + x.Value);

                    result[x.Key] = v;
                }
                else
                {
                    result[x.Key] = x.Value;
                }
            }

            return result;
        }

        public static SortedList<long, byte> Subtract(this SortedList<long, byte> vec1, SortedList<long, byte> vec2)
        {
            var result = new SortedList<long, byte>();

            foreach (var x in vec1)
            {
                byte val;

                if (vec2.TryGetValue(x.Key, out val) && val > 0)
                {
                    result[x.Key] = (byte)(val - 1);
                }
            }

            foreach (var x in vec2)
            {
                byte val;

                if (vec1.TryGetValue(x.Key, out val) && val > 0)
                {
                    result[x.Key] = (byte)(val - 1);
                }
            }
            return result;
        }

        public static SortedList<long, byte> ToVector(this Term term)
        {
            if (term.Node != null)
            {
                return term.Node.Vector;
            }

            var vec = new SortedList<long, byte>();
            var span = term.TokenizedString.Tokens[term.Index];

            for (int i = 0; i < span.length; i++)
            {
                var codePoint = (int)term.TokenizedString.Source[span.offset + i];

                if (vec.ContainsKey(codePoint))
                {
                    if (vec[codePoint] < byte.MaxValue) vec[codePoint] += 1;
                }
                else
                {
                    vec[codePoint] = 1;
                }
            }
            
            return vec;
        }

        public static SortedList<long, byte> ToCharVector(this AnalyzedString term, int offset, int length)
        {
            var vec = new SortedList<long, byte>();

            for (int i = 0; i < length; i++)
            {
                var codePoint = (int)term.Source[offset + i];

                if (vec.ContainsKey(codePoint))
                {
                    if (vec[codePoint] < byte.MaxValue) vec[codePoint] += 1;
                }
                else
                {
                    vec[codePoint] = 1;
                }
            }

            return vec;
        }

        public static SortedList<long, byte> ToCharVector(this string word)
        {
            var vec = new SortedList<long, byte>();
            TextElementEnumerator charEnum = StringInfo.GetTextElementEnumerator(word);

            while (charEnum.MoveNext())
            {
                var element = charEnum.GetTextElement().ToCharArray();
                int codePoint = 0;

                foreach (char c in element)
                {
                    codePoint += c;
                }

                if (vec.ContainsKey(codePoint))
                {
                    if (vec[codePoint] < byte.MaxValue) vec[codePoint] += 1;
                }
                else
                {
                    vec[codePoint] = 1;
                }
            }
            return vec;
        }

        public static float Magnitude(this SortedList<long, byte> vector)
        {
            return (float) Math.Sqrt(Dot(vector, vector));
        }

        public static SortedList<long, byte> CreateDocumentVector(
            IEnumerable<SortedList<long, byte>> termVectors, 
            NodeReader reader, 
            ITokenizer tokenizer)
        {
            var docVec = new SortedList<long, byte>();

            foreach (var term in termVectors)
            {
                var hit = reader.ClosestMatch(term);
                var offset = hit.Node.PostingsOffsets == null ? hit.Node.PostingsOffsets[0] : hit.Node.PostingsOffset;

                if (hit.Score == 0 || offset < 0)
                {
                    throw new DataMisalignedException();
                }

                var termId = offset;

                if (docVec.ContainsKey(termId))
                {
                    if (docVec[termId] < byte.MaxValue) docVec[termId] += 1;
                }
                else
                {
                    docVec.Add(termId, 1);
                }
            }

            return docVec;
        }

        public static bool ContainsMany(this string text, char c)
        {
            var vector = text.ToCharVector();

            if (vector[c] > 1)
            {
                return true;
            }

            return false;
        }
    }
}
