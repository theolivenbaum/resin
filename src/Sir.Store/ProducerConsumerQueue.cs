using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class ProducerConsumerQueue<T> : IDisposable where T : class
    {
        private readonly BlockingCollection<T> _queue;

        public ProducerConsumerQueue(Action<T> consumingAction)
        {
            _queue = new BlockingCollection<T>();

            Task.Run(() =>
            {
                while (!_queue.IsCompleted)
                {
                    T item = null;
                    try
                    {
                        item = _queue.Take();
                    }
                    catch (InvalidOperationException) { }

                    if (item != null)
                    {
                        consumingAction(item);
                    }
                }
            });
        }

        public void Enqueue(T item)
        {
            _queue.Add(item);
        }

        public void Dispose()
        {
            _queue.CompleteAdding();

            while (!_queue.IsCompleted)
            {
                Thread.Sleep(10);
            }

            _queue.Dispose();
        }
    }
}
