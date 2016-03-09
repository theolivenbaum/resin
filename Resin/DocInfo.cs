namespace Resin
{
    public class DocInfo
    {
        public int Id { get; set; }
        public int Occasions { get; set; }
    }

    public class DocFile
    {
        public string FileName { get; set; }
        public string Field { get; set; }
        public string Value { get; set; }
        public int DocId { get; set; }
    }
}