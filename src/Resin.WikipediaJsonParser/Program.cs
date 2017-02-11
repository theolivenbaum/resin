using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Resin.WikipediaJsonParser
{
    class Program
    {
        //D:\wikipedia\latest-all.json c:\temp\1wikipedia.json 1000000 2000000
        private static void Main(string[] args)
        {
            var timer = new Stopwatch();
            timer.Start();

            var fileName = args[0];
            var destination = args[1];
            var skip = int.Parse(args[2]);
            var length = int.Parse(args[3]);
            var count = 0;

            using (var ws = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(ws, Encoding.Unicode))
            {
                var cursorPos = Console.CursorLeft;
                
                w.WriteLine('[');

                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var bs = new BufferedStream(fs))
                using (var sr = new StreamReader(bs, Encoding.UTF8))
                {
                    sr.ReadLine();
                    var line = sr.ReadLine();

                    while (line != null)
                    {
                        if (count++ < skip)
                        {
                            line = sr.ReadLine();
                            continue;
                        }

                        if (line[0] == ']' || count == length)
                        {
                            break;
                        }

                        var source = JObject.Parse(line.Substring(0, line.Length - 1));

                        if ((string) source["type"] != "item")
                        {
                            line = sr.ReadLine();
                            continue;
                        }

                        var labelsToken = source["labels"]["en"];
                        var labelToken = labelsToken == null ? null : labelsToken["value"];

                        if (labelToken == null)
                        {
                            line = sr.ReadLine();
                            continue;
                        }

                        var id = (string)source["id"];
                        var label = labelToken.Value<string>();
                        var descriptionToken = source["descriptions"]["en"];
                        var description = descriptionToken == null ? null : source["descriptions"]["en"]["value"].Value<string>();
                        var aliasesToken = source["aliases"]["en"];
                        var aliases = aliasesToken == null ? null : String.Join(" ", aliasesToken.Select(t => t["value"].Value<string>()));

                        var doc = JObject.FromObject(new
                        {
                            _id = id,
                            label,
                            description,
                            aliases
                        });

                        var docAsJsonString = doc.ToString(Formatting.None);

                        w.WriteLine(docAsJsonString + ",");

                        line = sr.ReadLine();

                        Console.SetCursorPosition(cursorPos, Console.CursorTop);
                        Console.Write(count);
                    }
                }

                w.WriteLine(']');
            }
            Console.WriteLine("\r\ndone in {0}", timer.Elapsed);
        }
    }
}
