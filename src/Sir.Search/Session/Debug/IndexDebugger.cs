using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;

namespace Sir.Search
{
    public class IndexDebugger
    {
        private readonly Stopwatch _time;
        private readonly int _sampleSize;
        private int _batchNo;
        private int _steps;
        private readonly ILogger _logger;

        public IndexDebugger(ILogger logger, int sampleSize = 1000)
        {
            _sampleSize = sampleSize;
            _time = Stopwatch.StartNew();
            _logger = logger;
        }

        public void Step(IIndexSession indexSession)
        {
            if (++_steps % _sampleSize == 0)
            {
                var info = indexSession.GetIndexInfo();
                var t = _time.Elapsed.TotalSeconds;
                var docsPerSecond = (int)(_sampleSize / t);
                var debug = string.Join('\n', info.Info.Select(x => x.ToString()));
                var message = $"\n{_time.Elapsed}\nbatch {_batchNo++}\n{debug}\n{docsPerSecond} docs/s";

                _logger.LogInformation(message);
                _time.Restart();
            }
        }
    }

    public class BatchDebugger
    {
        private readonly Stopwatch _time;
        private readonly int _sampleSize;
        private int _batchNo;
        private int _steps;

        public int StepCount => _steps;
        public TimeSpan Time => _time.Elapsed;

        public BatchDebugger(int sampleSize = 1000)
        {
            _sampleSize = sampleSize;
            _time = Stopwatch.StartNew();
        }

        public string Step()
        {
            if (++_steps % _sampleSize == 0)
            {
                var t = _time.Elapsed.TotalSeconds;
                var itemsPerSecond = (int)(_sampleSize / t);
                var message = $"\n{_time.Elapsed}\nbatch {_batchNo++}\n{itemsPerSecond} items/s";

                _time.Restart();

                return message;
            }

            return null;
        }
    }
}
