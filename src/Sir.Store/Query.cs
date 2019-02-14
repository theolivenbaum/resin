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
        public Query(Term term)
        {
            Term = term;
            PostingsOffsets = new List<long>();
            Score = -1;
            Or = true;
        }

        public Query(float score, IList<long> postingsOffsets)
        {
            Score = score;
            PostingsOffsets = postingsOffsets;
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
        public IList<long> PostingsOffsets { get; set; }
        public float Score { get; set; }

        public override string ToString()
        {
            var op = And ? "AND " : Or ? "OR " : "NOT ";

            return string.Format("{0}{1} {2}", op, Term, Score);
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
            var clauses = ToList();
            var result = new MemoryStream();

            for (int index = 0; index < clauses.Count; index++)
            {
                var q = clauses[index];

                byte termOperator = 0;

                if (q.And)
                {
                    termOperator = 1;
                }
                else if (q.Or)
                {
                    termOperator = 2;
                }

                result.Write(BitConverter.GetBytes(q.Score));
                result.WriteByte(termOperator);
                result.Write(BitConverter.GetBytes(q.PostingsOffsets.Count));

                foreach(var offs in q.PostingsOffsets)
                {
                    result.Write(BitConverter.GetBytes(offs));
                }

                var then = q.Then;

                while (then != null)
                {
                    termOperator = 100;

                    if (then.And)
                    {
                        termOperator = 101;
                    }
                    else if (then.Or)
                    {
                        termOperator = 102;
                    }

                    result.Write(BitConverter.GetBytes(then.Score));
                    result.WriteByte(termOperator);
                    result.Write(BitConverter.GetBytes(then.PostingsOffsets.Count));

                    foreach (var offs in then.PostingsOffsets)
                    {
                        result.Write(BitConverter.GetBytes(offs));
                    }

                    then = then.Then;
                }
            }

            return result.ToArray();
        }

        public void AddClause(Query query)
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