using System;
using System.Collections.Generic;
using System.Text;

namespace Sir.Store
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query
    {
        private static readonly object _sync = new object();

        public Query(Term term)
        {
            Term = term;
            PostingsOffset = -1;
            Score = -1;
            Or = true;
        }

        public Query(Hit hit)
        {
            Hit = hit;
            Score = hit.Score;
            PostingsOffset = hit.PostingsOffset;
            Or = true;
        }

        public ulong Collection { get; set; }
        public bool And { get; set; }
        public bool Or { get; set; }
        public bool Not { get; set; }
        public Term Term { get; private set; }
        public Query Next { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public Hit Hit { get; private set; }
        public long PostingsOffset { get; set; }
        public float Score { get; set; }

        public override string ToString()
        {
            var op = And ? "AND " : Or ? "OR " : "NOT ";

            if (Hit == null)
            {
                return string.Format("{0}{1} {2}", op, Term, Score);
            }

            return string.Format("{0}{1} {2}", op, Hit, Score);;
        }

        public string ToDiagram()
        {
            var x = this;
            var diagram = new StringBuilder();

            while (x != null)
            {
                diagram.AppendLine(x.ToString());

                x = x.Next;
            }

            return diagram.ToString();
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
            var result = new byte[list.Count * (sizeof(float) + sizeof(long) + sizeof(byte))];
            var offset = 0;

            for (int index = 0; index < list.Count; index++)
            {
                var q = list[index];

                if (q.PostingsOffset < 0)
                {
                    continue;
                }

                byte termOperator = 0;

                if (q.And)
                {
                    termOperator = 1;
                }
                else if (q.Or)
                {
                    termOperator = 2;
                }

                var pbuf = BitConverter.GetBytes(q.PostingsOffset);

                Buffer.BlockCopy(pbuf, 0, result, offset, pbuf.Length);

                offset += sizeof(long);

                var sbuf = BitConverter.GetBytes(q.Score);

                Buffer.BlockCopy(sbuf, 0, result, offset, sbuf.Length);

                offset += sizeof(float);

                result[offset] = termOperator;

                offset += sizeof(byte);
            }

            return result;
        }

        public void InsertAfter(Query query)
        {
            lock (_sync)
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
}