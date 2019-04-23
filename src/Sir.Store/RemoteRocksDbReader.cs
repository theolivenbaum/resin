using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class RemoteRocksDbReader
    {
        public async Task<byte[]> Read(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);

            request.ContentType = "application/rocksdb+octet-stream";
            request.Accept = "application/rocksdb+octet-stream";
            request.Method = WebRequestMethods.Http.Get;

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                using (var responseBody = response.GetResponseStream())
                {
                    var mem = new MemoryStream();

                    await responseBody.CopyToAsync(mem);

                    return mem.ToArray();
                }
            }
        }
    }
}
