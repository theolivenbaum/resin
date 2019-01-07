using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Write postings to HTTP endpoint.
    /// </summary>
    public class RemotePostingsWriter
    {
        private IConfigurationProvider _config;
        private readonly StreamWriter _log;

        public RemotePostingsWriter(IConfigurationProvider config)
        {
            _config = config;
            _log = Logging.CreateWriter("remotepostingswriter");
        }

        public async Task Write(ulong collectionId, VectorNode rootNode)
        {
            var timer = Stopwatch.StartNew();

            IList<VectorNode> nodes;
            byte[] payload;

            // create postings message

            using (var message = new MemoryStream())
            using (var lengths = new MemoryStream())
            using (var offsets = new MemoryStream())
            using (var documents = new MemoryStream())
            {
                // Write length of word (i.e. length of list of postings) to header stream,
                // postings offsets to offset stream,
                // and word itself to documents stream.
                nodes = rootNode.SerializePostings(lengths, offsets, documents);

                if (nodes.Count == 0)
                    return;

                if (nodes.Count != lengths.Length / sizeof(int))
                {
                    throw new DataMisalignedException();
                }

                // first word of message is payload count (i.e. num of postings lists)
                await message.WriteAsync(BitConverter.GetBytes(nodes.Count));

                // next are lengths
                lengths.Position = 0;
                await lengths.CopyToAsync(message);

                // then all of the offsets
                offsets.Position = 0;
                await offsets.CopyToAsync(message);

                // last are the document IDs
                documents.Position = 0;
                await documents.CopyToAsync(message);

                var buf = message.ToArray();
                var ctime = Stopwatch.StartNew();
                var compressed = QuickLZ.compress(buf, 3);

                _log.Log(string.Format("compressing {0} bytes to {1} took {2}", buf.Length, compressed.Length, ctime.Elapsed));

                payload = compressed;
            }

            _log.Log(string.Format("create postings message took {0}", timer.Elapsed));

            // send message, recieve list of (remote) file positions, save positions in index.

            var positions = await Send(collectionId, payload);

            if (nodes.Count != positions.Count)
            {
                throw new DataMisalignedException();
            }

            timer.Restart();

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].PostingsOffset = positions[i];
            }

            _log.Log(string.Format("record postings offsets took {0}", timer.Elapsed));

        }

        private async Task<IList<long>> Send(ulong collectionId, byte[] payload)
        {
            var timer = new Stopwatch();
            timer.Start();

            var result = new List<long>();

            var endpoint = _config.Get("postings_endpoint") + collectionId.ToString();

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.ContentType = "application/postings";
            request.Accept = "application/octet-stream";
            request.Method = WebRequestMethods.Http.Post;
            request.ContentLength = payload.Length;

            int responseBodyLen = 0;

            using (var requestBody = await request.GetRequestStreamAsync())
            {
                requestBody.Write(payload, 0, payload.Length);

                using (var response = (HttpWebResponse) await request.GetResponseAsync())
                {
                    using (var responseBody = response.GetResponseStream())
                    {
                        _log.Log(string.Format("sent {0} bytes and got a response in {1}", payload.Length, timer.Elapsed));
                        timer.Restart();

                        var mem = new MemoryStream();

                        await responseBody.CopyToAsync(mem);

                        var buf = mem.ToArray();

                        responseBodyLen = buf.Length;

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

                        _log.Log(string.Format("deserialized {0} bytes of response data in {1}", buf.Length, timer.Elapsed));
                    }
                }
            }

            return result;    
        }
    }
}
