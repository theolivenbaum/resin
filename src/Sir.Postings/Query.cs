using System;
using System.Collections.Generic;

namespace Sir.Postings
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query
    {
        public Query()
        {
            PostingsOffsets = new List<long>();
            Score = -1;
            Or = true;
        }

        public bool And { get; set; }
        public bool Or { get; set; }
        public bool Not { get; set; }
        public IList<long> PostingsOffsets { get; set; }
        public float Score { get; set; }
        public Query Then { get; set; }

        public static IList<Query> FromStream(byte[] stream)
        {
            var result = new List<Query>();
            var offset = 0;

            while (offset < stream.Length)
            {
                var score = BitConverter.ToSingle(stream, offset);

                offset += sizeof(float);

                var termOperator = stream[offset];

                offset += sizeof(byte);

                var count = BitConverter.ToInt32(stream, offset);

                offset += sizeof(int);

                var postingsOffsets = new List<long>();

                for (int i = 0; i < count; i++)
                {
                    postingsOffsets.Add(BitConverter.ToInt64(stream, offset));

                    offset += sizeof(long);
                }

                var query = new Query
                {
                    Score = score,
                    PostingsOffsets = postingsOffsets,
                    And = termOperator == 1 || termOperator == 101,
                    Or = termOperator == 2 || termOperator == 201,
                    Not = termOperator == 0 || termOperator == 100
                };

                if (termOperator < 100)
                {
                    result.Add(query);
                }
                else
                {
                    result[result.Count - 1].AddClause(query);
                }
            }

            return result;
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