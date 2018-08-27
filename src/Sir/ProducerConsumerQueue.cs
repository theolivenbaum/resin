using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Core
{
    /// <summary>
    /// Enque items and run a single consumer thread.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ProducerConsumerQueue<T> : IDisposable where T : class
    {
        private readonly BlockingCollection<T> _queue;
        private readonly int? _pause;
        private Task _consumer;

        public ProducerConsumerQueue(Action<T> consumingAction, int? pauseMs = null)
        {
            _queue = new BlockingCollection<T>();
            _pause = pauseMs;

            _consumer = Task.Run(() =>
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
                        if (_pause != null) Thread.Sleep(_pause.Value);

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

            _consumer.Wait();

            _queue.Dispose();
        }
    }
}
