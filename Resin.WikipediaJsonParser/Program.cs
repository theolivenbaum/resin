using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Resin.IO;

namespace Resin.WikipediaJsonParser
{
    class Program
    {
        private static void Main(string[] args)
        {
            var timer = new Stopwatch();
            timer.Start();
            var fileName = args[0];
            var destination = args[1];
            var skip = int.Parse(args[2]);
            var length = int.Parse(args[3]);
            var docs = new List<Document>();
            var count = 0;
            var cursorPos = Console.CursorLeft;
            foreach (var line in File.ReadLines(fileName).Skip(1+skip))
            {
                if (line[0] == ']') break;
                    
                var source = JObject.Parse(line.Substring(0, line.Length - 1));
                if((string)source["type"] != "item") continue;

                var id = (string) source["id"];
                var labelsToken = source["labels"]["en"];
                var labelToken = labelsToken == null ? null : labelsToken["value"];
                var label = labelToken == null ? null : labelToken.Value<string>();
                var descriptionToken = source["descriptions"]["en"];
                var description = descriptionToken == null ? null : source["descriptions"]["en"]["value"].Value<string>();
                var aliasesToken = source["aliases"]["en"];
                var aliases = aliasesToken == null ? null : String.Join(" ", aliasesToken.Select(t => t["value"].Value<string>()));
                var doc = new Dictionary<string, string>
                {
                    {"_id", id}, {"label", label}, {"description", description}, {"aliases", aliases}
                };
                docs.Add(new Document(doc));
                Console.SetCursorPosition(cursorPos, Console.CursorTop);
                Console.Write(++count);
                if(count == length) break;
            }
            if(File.Exists(destination)) File.Delete(destination);
            new DocFile(docs.ToDictionary(x => x.Id, y => y)).Save(destination);
            Console.WriteLine("\r\ndone in {0}", timer.Elapsed);
        }

    }
}
