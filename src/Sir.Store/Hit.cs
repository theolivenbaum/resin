using System.Linq;

namespace Sir.Store
{
    public class Hit
    {
        public float Score { get; set; }
        public VectorNode Node { get; set; }

        public override string ToString()
        {
            return string.Join(string.Empty, Node.Vector.Keys.Select(x => char.ConvertFromUtf32((int)x)).ToArray());
        }
    }
}
