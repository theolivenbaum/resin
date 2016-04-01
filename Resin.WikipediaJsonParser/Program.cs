using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            var count = 0;
            using (var w = File.CreateText(destination))
            {
                var cursorPos = Console.CursorLeft;
                w.WriteLine('[');
                foreach (var line in File.ReadLines(fileName).Skip(1+skip))
                {
                    if (line[0] == ']') break;
                    
                    var source = JObject.Parse(line.Substring(0, line.Length - 1));
                    if((string)source["type"] != "item") continue;

                    var _id = (string) source["id"];
                    var labelsToken = source["labels"]["en"];
                    var labelToken = labelsToken == null ? null : labelsToken["value"];
                    var label = labelToken == null ? null : labelToken.Value<string>();
                    var descriptionToken = source["descriptions"]["en"];
                    var description = descriptionToken == null ? null : source["descriptions"]["en"]["value"].Value<string>();
                    var aliasesToken = source["aliases"]["en"];
                    var aliases = aliasesToken == null ? null : String.Join(" ", aliasesToken.Select(t => t["value"].Value<string>()));
                    var doc = JObject.FromObject(new 
                    {
                        _id, label, description, aliases
                    });
                    var docAsJsonString = doc.ToString(Formatting.None);
                    w.WriteLine(docAsJsonString + ",");
                    Console.SetCursorPosition(cursorPos, Console.CursorTop);
                    Console.Write(++count);
                    if(count == length) break;
                    
                }
                w.WriteLine(']');
            }
            Console.WriteLine("\r\ndone in {0}", timer.Elapsed);
        }

    }
}
