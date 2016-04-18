using Resin.IO;

namespace Resin
{
    public class FieldWriter
    {
        // terms/docids/term frequency
        public FieldFile FieldFile { get; protected set; }

        // prefix tree
        public Trie Trie { get; protected set; }

        public FieldWriter()
        {
            FieldFile = new FieldFile();
            Trie = new Trie();
        }

        public void Write(string docId, string token, int frequency, bool analyzed)
        {
            FieldFile.AddOrOverwrite(docId, token, frequency);
            if (analyzed)
            {
                Trie.Add(token);
            }
        }
    }
}