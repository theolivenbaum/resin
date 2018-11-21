using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Sir.Core
{
    /// <summary>
    /// Enque items and forget about it. They will be consumed by other threads.
    /// Call ProducerConsumerQueue.Complete() to have consumer threads join main.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TaskProducerConsumerQueue : IDisposable
    {
        private readonly BlockingCollection<Task> _queue;
        private readonly int _numOfConsumers;
        private Task[] _consumers;
        private bool _completed;
        private bool _started;

        public TaskProducerConsumerQueue() : this(1) { }

        public TaskProducerConsumerQueue(int numOfConsumers)
        {
            _queue = new BlockingCollection<Task>();
            _numOfConsumers = numOfConsumers;

            Start();
        }

        private void Start()
        {
            if (_started)
                return;

            _consumers = new Task[_numOfConsumers];

            for (int i = 0; i < _numOfConsumers; i++)
            {
                _consumers[i] = Task.Run(async () =>
                {
                    while (!_queue.IsCompleted)
                    {
                        try
                        {
                            var item = _queue.Take();

                            await Task.Run(() => item);
                        }
                        catch (InvalidOperationException) { }
                    }
                });
            }

            _started = true;
        }

        public void Enqueue(Task item)
        {
            _queue.Add(item);
        }

        public void Join()
        {
            if (!_started)
                return;

            if (_completed)
                return;

            _queue.CompleteAdding();

            Task.WaitAll(_consumers);

            _queue.Dispose();

            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed)
            {
                Join();
            }
        }
    }
}
