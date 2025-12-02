using System.Collections.Concurrent;
using DeBillPay_Backend.Data;
using DeBillPay_Backend.DTOs;

namespace DeBillPay_Backend.Services
{
    public class EmailQueue
    {
        private readonly ConcurrentQueue<EmailTask> _queue = new();

        public void Enqueue(EmailTask task)
        {
            if (task != null)
                _queue.Enqueue(task);
        }

        public EmailTask? Dequeue()
        {
            _queue.TryDequeue(out var task);
            return task;
        }
    }
}