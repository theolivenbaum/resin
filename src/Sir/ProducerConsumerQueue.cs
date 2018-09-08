using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Core
{
    /// <summary>
    /// Enque items and forget about it. They will be consumed by another thread.
    /// Call ProducerConsumerQueue.Dispose() to have consumer thread join main.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ProducerConsumerQueue<T> : IDisposable
    {
        private readonly BlockingCollection<T> _queue;
        private Task _consumer;
        private bool _completed;

        public ProducerConsumerQueue(Action<T> consumingAction)
        {
            _queue = new BlockingCollection<T>();

            _consumer = Task.Run(() =>
            {
                while (!_queue.IsCompleted)
                {
                    try
                    {
                        var item = _queue.Take();

                        consumingAction(item);
                    }
                    catch (InvalidOperationException) { }
                }
            });
        }

        public void Enqueue(T item)
        {
            _queue.Add(item);
        }

        public void Complete()
        {
            _queue.CompleteAdding();
            _consumer.Wait();
            _queue.Dispose();

            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed)
            {
                Complete();
            }
        }
    }
}
