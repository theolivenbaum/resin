using System;
using System.Collections.Generic;
using log4net;
using NetSerializer;

namespace Resin.IO
{
    [Serializable]
    public abstract class BinaryFile
    {
        [NonSerialized]
        protected static readonly ILog Log = LogManager.GetLogger(typeof(BinaryFile));

        [NonSerialized]
        private static readonly Type[] Types =
        {
            typeof (string), 
            typeof (int), 
            typeof (char), 
            typeof (bool), 
            typeof (LcrsTrie), 
            typeof (DocumentCount),
            typeof (Dictionary<Term, int>),
            typeof (Dictionary<string, string>),
            typeof (Dictionary<string, int>),
            typeof (Document),
            typeof (IxInfo),
            typeof (List<DocumentPosting>)
        };

        [NonSerialized]
        public static readonly Serializer Serializer = new Serializer(Types);
    }
}