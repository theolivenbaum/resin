using System;
using System.Collections.Generic;

namespace Resin.IO
{
    public static class GraphSerializer
    {
        private static readonly Type[] Types =
        {
            typeof (string), 
            typeof (int), 
            typeof (List<int>),
            typeof (DocumentCount),
            typeof (Dictionary<string, int>),
            typeof (Document),
            typeof (Dictionary<string, string>),
            typeof (List<DocumentPosting>),
            typeof (BlockInfo),
            typeof (Document),
            typeof (List<DocumentPosting>)
        };

        [NonSerialized]
        public static readonly NetSerializer.Serializer Serializer = new NetSerializer.Serializer(Types);
    }
}