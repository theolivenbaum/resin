using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Write postings to HTTP endpoint.
    /// </summary>
    public class RemotePostingsWriter : ILogger
    {
        private IConfigurationProvider _config;
        private readonly string _collectionName;

        public RemotePostingsWriter(IConfigurationProvider config, string collectionName)
        {
            _config = config;
            _collectionName = collectionName;
        }

        //public async Task Concat(IDictionary<long, IList<long>> offsets)
        //{
        //    foreach (var offset in offsets)
        //    {
        //        var canonical = offset.Key;
        //        var batch = new Dictionary<long, IList<long>>();

        //        batch.Add(canonical, offset.Value);

        //        await ExecuteConcat(batch);
        //    }
        //}

        public void Concat(VectorNode rootNode)
        {
            var offsets = new Dictionary<long, IList<long>>();
            var all = rootNode.All();

            foreach (var node in all)
            {
                if (node.PostingsOffsets != null && node.PostingsOffsets.Count > 1)
                {
                    offsets.Add(node.PostingsOffset, node.PostingsOffsets);
                }
            }

            if (offsets.Count == 0)
            {
                return;
            }

            Concat(offsets);
        }

        public void Concat(IDictionary<long, IList<long>> offsets)
        {
            if (offsets == null) throw new ArgumentNullException(nameof(offsets));

            var timer = Stopwatch.StartNew();

            var requestMessage = new MemoryStream();
        
            foreach (var offset in offsets)
            {
                // write key
                requestMessage.Write(BitConverter.GetBytes(offset.Key));

                // write count
                requestMessage.Write(BitConverter.GetBytes(offset.Value.Count));

                // write data
                foreach (var offs in offset.Value)
                {
                    requestMessage.Write(BitConverter.GetBytes(offs));
                }
            }

            var messageBuf = requestMessage.ToArray();
            var compressed = QuickLZ.compress(messageBuf, 3);
            var endpoint = string.Format("{0}{1}?concat=concat", 
                _config.Get("postings_endpoint"), _collectionName);

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.Method = WebRequestMethods.Http.Post;
            request.ContentType = "application/postings";

            using (var requestBody = request.GetRequestStream())
            {
                requestBody.Write(compressed, 0, compressed.Length);

                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    this.Log(string.Format("{0} concat operation took {1}", _collectionName, timer.Elapsed));
                }
            }
        }

        public async Task Write(VectorNode rootNode)
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

                this.Log(string.Format("compressing {0} bytes to {1} took {2}", buf.Length, compressed.Length, ctime.Elapsed));

                payload = compressed;
            }

            this.Log(string.Format("create postings message took {0}", timer.Elapsed));

            // send message, recieve list of (remote) file positions, save positions in index.

            var positions = await Send(payload);

            if (nodes.Count != positions.Count)
            {
                throw new DataMisalignedException();
            }

            timer.Restart();

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].PostingsOffset = positions[i];
            }

            this.Log(string.Format("record postings offsets took {0}", timer.Elapsed));
        }

        private async Task<IList<long>> Send(byte[] payload)
        {
            var timer = new Stopwatch();
            timer.Start();

            var result = new List<long>();

            var endpoint = _config.Get("postings_endpoint") + _collectionName;

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
                        this.Log(string.Format("sent {0} bytes and got a response in {1}", payload.Length, timer.Elapsed));
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

                        this.Log(string.Format("deserialized {0} bytes of response data in {1}", buf.Length, timer.Elapsed));
                    }
                }
            }

            return result;    
        }
    }
}
