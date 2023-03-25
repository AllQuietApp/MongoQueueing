namespace AllQuiet.MongoQueueing
{
    public class GenericQueue : IGenericQueue
    {
        private readonly IQueue<GenericQueueEvent> queue;

        public GenericQueue(IQueue<GenericQueueEvent> queue)
        {
            this.queue = queue;
        }

        public async Task EnqueueAsync(object payload, DateTime? nextReevaluation = null)
        {
            await this.queue.EnqueueAsync(new GenericQueueEvent { Payload = payload }, nextReevaluation);
        }
    }

    public interface IGenericQueue
    {
        Task EnqueueAsync(object payload, DateTime? nextReevaluation = null);
    }
}