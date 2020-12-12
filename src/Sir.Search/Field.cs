using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sir.Search
{
    [DebuggerDisplay("{Name}")]
    public class Field
    {
        private VectorNode _tree;

        public long KeyId { get; set; }
        public string Name { get; }
        public object Value { get; set; }
        public bool Index { get; }
        public bool Store { get; }

        public Field(string name, object value, long keyId = -1, bool index = true, bool store = true)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Name = name;
            Value = value;
            Index = index;
            Store = store;
            KeyId = keyId;
        }

        public IEnumerable<IVector> GetTokens()
        {
            foreach (var node in PathFinder.All(_tree))
                yield return node.Vector;
        }

        public void Analyze(ITextModel model)
        {
            var tokens = model.Tokenize((string)Value);
            
            _tree = new VectorNode();

            foreach (var token in tokens)
            {
                model.ExecutePut<string>(_tree, KeyId, new VectorNode(token));
            }
        }
    }
}