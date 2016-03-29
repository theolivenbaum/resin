using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class FieldFile : FileBase<FieldFile>
    {
        // terms/docids/term frequency
        [ProtoMember(1)]
        private readonly IDictionary<string, IDictionary<int, int>> _terms;

        public FieldFile()
        {
            _terms = new Dictionary<string, IDictionary<int, int>>();
        }

        public IDictionary<string, IDictionary<int, int>> Terms
        {
            get { return _terms; }
        }
    }
}