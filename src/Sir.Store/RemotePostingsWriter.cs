using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IList<long> Write(string collectionId, IList<KeyValuePair<long, VectorNode>> rootNodes)
        {
            var nodes = new List<VectorNode>();
            byte[] payload;

            // create postings message

            using (var message = new MemoryStream())
            using (var header = new MemoryStream())
            using (var body = new MemoryStream())
            {
                foreach (var node in rootNodes)
                {
                    // write length of word (i.e. length of list of postings) to header 
                    // and word itself to body
                    var dirty = node.Value.SerializePostings(
                            collectionId,
                            header,
                            body)
                            .ToList();

                    nodes.AddRange(dirty);
                }

                if (nodes.Count != header.Length / sizeof(int))
                {
                    throw new DataMisalignedException();
                }

                // first word of message is payload count (i.e. num of words (i.e. posting lists))
                message.Write(BitConverter.GetBytes(nodes.Count));

                // next block is header
                header.Position = 0;
                header.CopyTo(message);

                // last block is body
                body.Position = 0;
                body.CopyTo(message);

                payload = message.ToArray();
            }

            // send message, recieve list of (remote) file positions
            var positions = Send(collectionId, payload);

            if (nodes.Count != positions.Count)
            {
                throw new DataMisalignedException();
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].PostingsOffset = positions[i];
            }

            return positions;
        }

        private IList<long> Send(string collectionId, byte[] payload)
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
