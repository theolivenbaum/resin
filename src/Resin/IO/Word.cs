namespace Resin.IO
{
    public struct Word
    {
        public readonly string Value;
        public readonly BlockInfo PostingsAddress;

        public Word(string value) : this(value, BlockInfo.MinValue) { }

        public Word(string value, BlockInfo postingsAddress)
        {
            Value = value;
            PostingsAddress = postingsAddress;
        }

        public static Word MinValue { get { return new Word(string.Empty, BlockInfo.MinValue);} }

        public override string ToString()
        {
            return Value;
        }
    }
}