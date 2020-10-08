using System.Diagnostics;
using System.Linq;

namespace Sir.Search
{
    public class IndexDebugger
    {
        private readonly Stopwatch _time;
        private readonly int _batchSize;
        private int _batchNo;
        private int _count;

        public IndexDebugger(int batchSize = 1000)
        {
            _time = Stopwatch.StartNew();
            _batchNo = 0;
            _count = 0;
            _batchSize = batchSize;
        }

        public string GetDebugInfo(IIndexSession indexSession)
        {
            if (_count++ == _batchSize)
            {
                var info = indexSession.GetIndexInfo();
                var t = _time.Elapsed.TotalSeconds;
                var docsPerSecond = (int)(_batchSize / t);
                var debug = string.Join('\n', info.Info.Select(x => x.ToString()));
                var message = $"\n{_time.Elapsed}\nbatch {++_batchNo}\n{debug}\n{docsPerSecond} docs/s";

                _count = 0;
                _time.Restart();

                return message;
            }

            return null;
        }
    }
}
