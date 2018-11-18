using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Sir.Store
{
    public class RemotePostingsWriter
    {
        private IConfigurationService _config;

        public RemotePostingsWriter(IConfigurationService config)
        {
            _config = config;
        }

        public IList<long> Write(string collectionId, byte[] payload)
        {
            var result = new List<long>();

            var endpoint = _config.Get("postings_endpoint") + collectionId;

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.ContentType = "application/postings";
            request.Accept = "application/octet-stream";
            request.Method = WebRequestMethods.Http.Post;
            request.ContentLength = payload.Length;

            using (var requestBody = request.GetRequestStream())
            {
                requestBody.Write(payload, 0, payload.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (var responseBody = response.GetResponseStream())
                    {
                        var mem = new MemoryStream();
                        responseBody.CopyTo(mem);
                        var buf = mem.ToArray();

                        if (buf.Length != response.ContentLength)
                        {
                            throw new DataMisalignedException();
                        }

                        int read = 0;

                        while (read < response.ContentLength)
                        {
                            result.Add(BitConverter.ToInt64(buf, read));

                            read += sizeof(long);
                        }
                    }
                }
            }

            return result;    
        }

        
    }
}
