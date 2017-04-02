using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Resin.IO
{
    public class IxInfo
    {
        public virtual string Name { get; set; }
        public virtual Dictionary<string, int> DocumentCount { get; set; }

        public static IxInfo Load(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fs, Encoding.Unicode))
            {
                var ix = new IxInfo();
                ix.Name = reader.ReadLine();
                ix.DocumentCount = new Dictionary<string, int>();

                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    var key = line;
                    var value = int.Parse(reader.ReadLine());

                    ix.DocumentCount.Add(key, value);
                }
                return ix;
            }
        }
    }
}