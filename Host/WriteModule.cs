using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net;
using Nancy;
using Nancy.ModelBinding;
using Newtonsoft.Json;

namespace Resin
{
    public class WriteModule : NancyModule
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WriteModule));

        public WriteModule()
        {
            Post["/{indexName}/add"] = parameters =>
            {
                var docs = this.Bind<Dictionary<string, string>[]>();
                var indexName = parameters.indexName;
                var timer = new Stopwatch();
                timer.Start();
                HandleRequest(indexName, docs);
                Log.InfoFormat("added {0} docs in {1}", docs.Length, timer.Elapsed);
                return HttpStatusCode.NoContent;
            };
        }

        private void HandleRequest(string indexName, IEnumerable<Dictionary<string, string>> docs)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();
                var dir = Path.Combine(Helper.GetResinDataDirectory(), indexName);
                using (var writer = new IndexWriter(dir, new Analyzer()))
                {
                    foreach (var doc in docs)
                    {
                        writer.Write(doc);
                        Log.DebugFormat("added doc {0} in {1}", doc["_id"], timer.Elapsed);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }
    }

    public class StringDictionaryBinder : IModelBinder
    {
        public object Bind(NancyContext context, Type modelType, object instance, BindingConfig configuration, params string[] blackList)
        {
            return DeserializeFromStream(context.Request.Body);
        }

        private static object DeserializeFromStream(Stream stream)
        {
            var serializer = new JsonSerializer();
            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                var obj = serializer.Deserialize<Dictionary<string, string>[]>(jsonTextReader);
                return obj;
            }
        }

        public bool CanBind(Type modelType)
        {
            var can = modelType.BaseType == typeof (Array);
            return can;
        }
    }
}