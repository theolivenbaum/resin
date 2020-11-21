namespace Sir.HttpServer
{
    public class HttpApiResponseModel
    {
        public long Total { get; set; }
        public string MediaType { get; set; }
        public byte[] Body { get; set; }
    }
}