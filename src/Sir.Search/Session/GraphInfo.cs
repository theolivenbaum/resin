using Sir.VectorSpace;

namespace Sir.Store
{
    public class GraphInfo
    {
        private readonly long _keyId;
        private readonly long _indexId;
        private readonly VectorNode _graph;

        public long Weight => _graph.Weight;

        public GraphInfo(long keyId, long indexId, VectorNode graph)
        {
            _keyId = keyId;
            _indexId = indexId;
            _graph = graph;
        }

        public override string ToString()
        {
            return $"key {_keyId} level {_graph.Level} indexId: {_indexId} weight {_graph.Weight} {PathFinder.Size(_graph)}";
        }
    }


}