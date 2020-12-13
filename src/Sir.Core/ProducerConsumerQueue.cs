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
    public class ProducerConsumerQueue<T> : IDisposable
    {
        private BlockingCollection<T> _queue;
        private readonly int _numOfConsumers;
        private readonly Action<T> _consumingAction;
        private Task[] _consumers;
        private bool _started;
        private bool _joining;

        public int Count { get { return _queue.Count; } }

        public bool IsCompleted { get { return _queue == null || _queue.IsCompleted; } }

        public ProducerConsumerQueue(Action<T> consumingAction, int numOfConsumers = 1)
        {
            if (consumingAction == null)
            {
                throw new ArgumentNullException(nameof(consumingAction));
            }

            _queue = new BlockingCollection<T>();
            _numOfConsumers = numOfConsumers;
            _consumingAction = consumingAction;

            Init();
        }

        private void Init()
        {
            if (_started)
                return;

            _consumers = new Task[_numOfConsumers];

            for (int i = 0; i < _numOfConsumers; i++)
            {
                _consumers[i] = Task.Run(() =>
                {
                    try
                    {
                        while (true)
                        {
                            var item = _queue.Take();

                            _consumingAction(item);
                        }
                    }
                    catch (InvalidOperationException) { }
                });
            }

            _started = true;
        }

        public void Enqueue(T item)
        {
            _queue.Add(item);
        }

        public void Join()
        {
            if (_joining || _queue.IsCompleted || !_started)
                return;

            _joining = true;

            _queue.CompleteAdding();
            Task.WaitAll(_consumers);
        }

        public void Dispose()
        {
            Join();
            _queue.Dispose();
        }
    }

    public class ProducerConsumerQueueCollection<T> : IDisposable
    {
        private readonly ConcurrentDictionary<long, ProducerConsumerQueue<T>> _queues;
        private readonly int _numOfConsumers;
        private readonly Action<T> _consumingAction;

        public ProducerConsumerQueueCollection(Action<T> consumingAction, int numOfConsumers = 1)
        {
            if (consumingAction == null)
            {
                throw new ArgumentNullException(nameof(consumingAction));
            }

            _numOfConsumers = numOfConsumers;
            _consumingAction = consumingAction;
            _queues = new ConcurrentDictionary<long, ProducerConsumerQueue<T>>();
        }

        public void Enqueue(long keyId, T item)
        {
            var queue = _queues.GetOrAdd(keyId, new ProducerConsumerQueue<T>(_consumingAction, _numOfConsumers));
            queue.Enqueue(item);
        }

        public void Dispose()
        {
            foreach (var queue in _queues.Values)
            {
                queue.Dispose();
            }
        }
    }
}
