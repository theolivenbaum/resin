using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin.SearchServer
{
    public static class SearchHitHelper
    {
        public static IEnumerable<SearchHit> ToSearchHits(this IEnumerable<ScoredDocument> documents)
        {
            return documents.Select(d => new SearchHit
            {
                Title = d.TableRow.Fields["title"].Value,
                Body = d.TableRow.Fields["body"].Value,
                Uri = d.TableRow.Fields["uri"].Value,
                //DisplayUrl = new Uri(d.Document.Fields["uri"].Value).ToDisplayUrl()
            });
        }

        public static string ToDisplayUrl(this Uri uri)
        {
            var segs = uri.Segments.Take(3).ToList();
            segs.Insert(0, uri.Host);
            return string.Join("", segs);
        }

        public static string Highlight(this string text, string[] highlight, int len)
        {
            var output = new HashSet<string>();
            var segLen = len / highlight.Length;

            foreach(var word in highlight)
            {
                var quote = text.Clean().Highlight(word, segLen);
                output.Add(quote);
            }

            return string.Join(" ... ", output).PadRight(len).Substring(0, len);
        }

        public static string Clean(this string text)
        {
            return text.Replace('.', ' ').Replace(',', ' ').Replace('-', ' ');
        }

        public static string Highlight(this string text, string token, int len)
        {
            var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);

            if (index < 0) index = 0;

            return text.Substring(index, Math.Min(len, text.Length-index));
        }
    }
}
