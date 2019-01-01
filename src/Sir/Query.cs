using System;
using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query
    {
        public Query()
        {
            PostingsOffset = -1;
            Score = -1;
            Or = true;
        }

        public Query(IComparable key, IComparable value)
        {
            PostingsOffset = -1;
            Term = new Term(key, value);
            Score = -1;
            Or = true;
        }

        public ulong Collection { get; set; }
        public bool And { get; set; }
        public bool Or { get; set; }
        public bool Not { get; set; }
        public Term Term { get; set; }
        public Query Next { get; set; }
        public int Take { get; set; }
        public long PostingsOffset { get; set; }
        public float Score { get; set; }

        public override string ToString()
        {
            var op = And ? "+" : Or ? " " : "-";
            return string.Format("{0}{1}", op, Term);
        }

        public IList<Query> ToList()
        {
            var list = new List<Query>();
            Query q = this;

            while (q != null)
            {
                list.Add(q);
                q = q.Next;
            }

            return list;
        }

        public byte[] ToStream()
        {
            var list = ToList();
            var result = new byte[(list.Count * (sizeof(float) + sizeof(long))) + (list.Count * sizeof(byte))];
            var offset = 0;

            for (int index = 0; index < list.Count; index++)
            {
                var q = list[index];

                if (q.PostingsOffset < 0)
                {
                    continue;
                }

                byte booleanOperator = 0;

                if (q.And)
                {
                    booleanOperator = 1;
                }
                else if (q.Or)
                {
                    booleanOperator = 2;
                }

                var pbuf = BitConverter.GetBytes(q.PostingsOffset);

                Buffer.BlockCopy(pbuf, 0, result, offset, pbuf.Length);

                offset += sizeof(long);

                var sbuf = BitConverter.GetBytes(Score);

                Buffer.BlockCopy(sbuf, 0, result, offset, sbuf.Length);

                offset += sizeof(float);

                result[offset] = booleanOperator;

                offset += sizeof(byte);
            }

            return result;
        }

        public static IEnumerable<Query> FromStream(byte[] stream)
        {
            var offset = 0;

            while (offset < stream.Length)
            {
                var postingsOffset = BitConverter.ToInt64(stream, offset);

                offset += sizeof(long);

                var score = BitConverter.ToSingle(stream, offset);

                offset += sizeof(float);

                var booleanOperator = stream[offset];

                offset += sizeof(byte);

                var query = new Query { Score = score, PostingsOffset = postingsOffset, And = booleanOperator == 1, Or = booleanOperator == 2, Not = booleanOperator == 0 };

                yield return query;
            }
        }

        public void InsertAfter(Query query)
        {
            if (Next == null)
            {
                Next = query;
            }
            else
            {
                var tmp = Next;
                Next = query;
                query.Next = tmp;
            }
        }
    }
}