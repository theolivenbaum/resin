using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.HttpServer.Features
{
    public class Conversation
    {
        private readonly IReader _reader;

        public Conversation(IReader reader)
        {
            _reader = reader;
        }

        public virtual string Start(string query)
        {
            string response;

            if (query.Contains("teddy"))
            {
                response = query.Replace("du", "").Replace("you", "").Replace("'re", "is").Replace("are", "is") + "?";

                if (!response.Contains("teddy"))
                {
                    response = $"teddy{response.Trim()}";
                }
            }
            else if (query.Contains("?"))
            {
                response = "Who knows, right? And who cares? Not me! Well, I gatz to go. Laters!";
            }
            else if (query.Contains("yes"))
            {
                response = "No, that's not right.";
            }
            else if (query.Contains("no"))
            {
                response = "No. I'm right, you're wrong. Probably. I'm pretty sure.";
            }
            else
            {
                response = "Not right now. I'm busy doing [insert_important_sounding_thing].";
            }

            return response;
        }
    }
}
