using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    public static class LcrsTrieHelper
    {
        public static IEnumerable<LcrsTrie> Fold(this IEnumerable<LcrsTrie> nodes, int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException("size");

            var count = 0;

            foreach (var child in nodes.ToList())
            {
                if (count == 0)
                {
                    yield return child;
                }
                else if (count == size)
                {
                    child.RightSibling = null;

                    count = -1;
                }
                count++;
            }
        }
    }
}