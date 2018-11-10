using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Sir.Store
{
    public class RemotePostingsReader
    {
        private IConfigurationService _config;

        public RemotePostingsReader(IConfigurationService config)
        {
            _config = config;
        }

        public IList<ulong> Read(string collectionId, long offset)
        {
            var endpoint = _config.Get("postings_endpoint");
            var request = (HttpWebRequest)WebRequest.Create(endpoint + collectionId);
            request.ContentType = "application/postings";
            request.Method = WebRequestMethods.Http.Get;

            var response = (HttpWebResponse)request.GetResponse();
            var result = new List<ulong>();

            using (var body = response.GetResponseStream())
            {
                var mem = new MemoryStream();
                body.CopyTo(mem);

                var buf = mem.ToArray();

                var read = 0;

                while (read < buf.Length)
                {
                    result.Add(BitConverter.ToUInt64(buf, read));

                    read += sizeof(ulong);
                }
            }

            return result;
        }
    }
}
