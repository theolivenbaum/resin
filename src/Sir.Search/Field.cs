using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sir.Search
{
    [DebuggerDisplay("{Name}")]
    public class Field
    {
        public VectorNode Tree { get; private set; }
        public long KeyId { get; set; }
        public long DocumentId { get; set; }
        public string Name { get; }
        public object Value { get; set; }
        public bool Index { get; }
        public bool Store { get; }

        public Field(string name, object value, long keyId = -1, bool index = true, bool store = true, long documentId = -1)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Name = name;
            Value = value;
            Index = index;
            Store = store;
            KeyId = keyId;
            DocumentId = documentId;
        }

        public IEnumerable<IVector> GetTokens()
        {
            foreach (var node in PathFinder.All(Tree))
                yield return node.Vector;
        }

        public void Analyze(ITextModel model)
        {
            var tokens = model.Tokenize((string)Value);

            Tree = new VectorNode();

            foreach (var token in tokens)
            {
                model.ExecutePut<string>(Tree, KeyId, new VectorNode(token));
            }
        }
    }
}