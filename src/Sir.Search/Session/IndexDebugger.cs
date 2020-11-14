﻿using System.Diagnostics;
using System.Linq;

namespace Sir.Search
{
    public class IndexDebugger
    {
        private readonly Stopwatch _time;
        private readonly int _sampleSize;
        private int _batchNo;
        private int _steps;

        public IndexDebugger(int sampleSize = 1000)
        {
            _sampleSize = sampleSize;
            _time = Stopwatch.StartNew();
        }

        public string Step(IIndexSession indexSession)
        {
            if (_steps++ % _sampleSize == 0)
            {
                var info = indexSession.GetIndexInfo();
                var t = _time.Elapsed.TotalSeconds;
                var docsPerSecond = (int)(_sampleSize / t);
                var debug = string.Join('\n', info.Info.Select(x => x.ToString()));
                var message = $"\n{_time.Elapsed}\nbatch {_batchNo++}\n{debug}\n{docsPerSecond} docs/s";

                _time.Restart();

                return message;
            }

            return null;
        }
    }
}
