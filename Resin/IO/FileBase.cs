using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using NetSerializer;

namespace Resin.IO
{
    public class FileBase<T>
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public virtual void Save(string fileName)
        {
            try
            {
                using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Serializer.Serialize(fs, this);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Unregistered type {0}", typeof(T)), ex);
            }
        }

        public static T Load(string fileName)
        {
            Log.DebugFormat("trying to load {0}", fileName);
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Log.DebugFormat("found {0}", fileName);
                var result = (T)Serializer.Deserialize(fs);
                Log.DebugFormat("loaded {0}", fileName);
                return result;
            }
        }

        private static readonly Type[] Types = new[]
        {
            typeof (string), typeof (int), typeof (char), typeof (Trie), typeof (Document), typeof (Trie),
            typeof (Dictionary<string, string>), typeof (Dictionary<string, Document>),
            typeof (Dictionary<string, Dictionary<string, int>>), typeof(Dictionary<char, Trie>),
            typeof(DixFile), typeof(DocFile), typeof(FieldFile), typeof(FixFile), typeof(IxFile)
        };

        private static readonly Serializer Serializer = new Serializer(Types);
    }
}