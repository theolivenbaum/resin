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
                var doc = this.Bind<Dictionary<string, string>>();
                var indexName = parameters.indexName;
                HandleRequest(indexName, doc);
                return HttpStatusCode.NoContent;
            };
        }

        private void HandleRequest(string indexName, Dictionary<string, string> doc)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();
                var dir = Path.Combine(Helper.GetResinDataDirectory(), indexName);
                using (var writer = new IndexWriter(dir, new Analyzer()))
                {
                    writer.Write(doc);
                }
                Log.InfoFormat("added doc {0} in {1}", doc["_id"], timer.Elapsed);
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
                return serializer.Deserialize<Dictionary<string, string>>(jsonTextReader);
            }
        }

        public bool CanBind(Type modelType)
        {
            // http://stackoverflow.com/a/16956978/39605
            if (modelType.IsGenericType && modelType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (modelType.GetGenericArguments()[0] == typeof(string) &&
                    modelType.GetGenericArguments()[1] == typeof(string))
                {
                    return true;
                }
            }

            return false;
        }
    }
}