using System.IO;
using CSharpTest.Net.Serialization;

namespace Resin.IO
{
    public class TermSerializer : ISerializer<Term>
    {
        public void WriteTo(Term value, Stream stream)
        {
            PrimitiveSerializer.String.WriteTo(value.Field, stream);
            PrimitiveSerializer.String.WriteTo(value.Word.Value, stream);
        }

        public Term ReadFrom(Stream stream)
        {
            var field = PrimitiveSerializer.String.ReadFrom(stream);
            var word = PrimitiveSerializer.String.ReadFrom(stream);

            return new Term(field, new Word(word));
        }
    }
}