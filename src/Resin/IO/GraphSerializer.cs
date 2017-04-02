using System;
using System.Collections.Generic;
using log4net;
using NetSerializer;

namespace Resin.IO
{
    [Serializable]
    public abstract class GraphSerializer
    {
        [NonSerialized]
        protected static readonly ILog Log = LogManager.GetLogger(typeof(GraphSerializer));

        [NonSerialized]
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