using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class FieldFile : FileBase<FieldFile>
    {
        // terms/docids/term frequency
        [ProtoMember(1)]
        private readonly IDictionary<string, IDictionary<string, int>> _terms;

        public FieldFile()
        {
            _terms = new Dictionary<string, IDictionary<string, int>>();
        }

        public IDictionary<string, IDictionary<string, int>> Terms
        {
            get { return _terms; }
        }
    }
}