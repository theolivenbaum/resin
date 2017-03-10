using System;
using System.Collections.Generic;
using log4net;
using NetSerializer;
using Resin.IO.Read;

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
            typeof (char), 
            typeof (bool), 
            typeof (DocumentCount),
            typeof (Dictionary<string, string>),
            typeof (Dictionary<string, int>),
            typeof (Document),
            typeof (IxInfo),
            typeof (List<string>),
            typeof (List<int>),
            typeof (List<DocumentPosting>),
            typeof (Term),
            typeof (Word),
            typeof(LcrsNode)
        };

        [NonSerialized]
        public static readonly Serializer Serializer = new Serializer(Types);
    }
}