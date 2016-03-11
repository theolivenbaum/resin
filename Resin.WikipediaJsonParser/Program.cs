using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Resin.WikipediaJsonParser
{
    class Program
    {
        private static void Main(string[] args)
        {
            var fileName = args[0];
            var skip = int.Parse(args[1]);
            var length = int.Parse(args[2]);
            using (var w = File.CreateText(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName) + "_resin.json")))
            {
                var count = 0;
                w.WriteLine('[');
                foreach (var line in File.ReadLines(fileName).Skip(1 + skip))
                {
                    if (line[0] == ']') break;
                    if (++count == length) break;
                    
                    var source = JObject.Parse(line.Substring(0, line.Length - 1));
                    if((string)source["type"] != "item") continue;

                    var docId = count + skip;
                    var id = new[]{(string) source["id"]};
                    var labelsToken = source["labels"]["en"];
                    var labelToken = labelsToken == null ? null : labelsToken["value"];
                    var label = labelToken == null ? new string[0] : new[]{labelToken.Value<string>()};
                    var descriptionToken = source["descriptions"]["en"];
                    var description = descriptionToken == null ? new string[0] : new[]{source["descriptions"]["en"]["value"].Value<string>()};
                    var aliasesToken = source["aliases"]["en"];
                    var aliases = aliasesToken == null ? new string[0] : new []{String.Join(" ", aliasesToken.Select(t => t["value"].Value<string>()))};
                    var fields = JObject.FromObject(new 
                    {
                        id, label, description, aliases
                    });
                    var doc = new JObject();
                    doc.Add("Fields", fields);
                    doc.Add("Id", docId);
                    var docAsJsonString = doc.ToString();
                    w.WriteLine(docAsJsonString + ",");
                    Console.WriteLine(docAsJsonString);
                    
                }
                w.WriteLine(']');
            }
        }
    }
}
