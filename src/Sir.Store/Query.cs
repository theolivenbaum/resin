using System;
using System.Collections.Generic;
using System.IO;
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
        public Query Then { get; set; }
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
            var result = new MemoryStream();

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

                result.Write(BitConverter.GetBytes(q.PostingsOffset));
                result.Write(BitConverter.GetBytes(q.Score));
                result.WriteByte(termOperator);

                var then = q.Then;

                while (then != null)
                {
                    termOperator = 100;

                    if (q.And)
                    {
                        termOperator = 101;
                    }
                    else if (q.Or)
                    {
                        termOperator = 102;
                    }

                    result.Write(BitConverter.GetBytes(q.PostingsOffset));
                    result.Write(BitConverter.GetBytes(q.Score));
                    result.WriteByte(termOperator);

                    then = then.Then;
                }
            }

            return result.ToArray();
        }

        public void AddClause(Query query)
        {
            lock (_sync)
            {
                if (Then == null)
                {
                    Then = query;
                }
                else
                {
                    Then.AddClause(query);
                }
            }
        }
    }
}