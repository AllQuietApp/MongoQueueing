namespace AllQuiet.MongoQueueing
{
    public class GenericQueue : IGenericQueue
    {
        private readonly IQueue<GenericQueueEvent> queue;

        public GenericQueue(IQueue<GenericQueueEvent> queue)
        {
            this.queue = queue;
        }

        public async Task<QueuedItem<GenericQueueEvent>> EnqueueAsync(object payload, DateTime? nextReevaluation = null)
        {
            return await this.queue.EnqueueAsync(new GenericQueueEvent { Payload = payload }, nextReevaluation);
        }
    }

    public interface IGenericQueue
    {	
        /// <summary>
        /// Enqueues a new payload on the generic queue.
        /// </summary>
        /// <param name="payload">The payload to enqueue.</param>
        /// <param name="nextReevaluation">If null, then the payload is processed as soon as possible. If specified, the payload will be processed at earliest after the provided DateTime. Use this param to schedule processing in the future.</param>
        /// <returns>The queued item with the attached payload.</returns>
        Task<QueuedItem<GenericQueueEvent>> EnqueueAsync(object payload, DateTime? nextReevaluation = null);
    }
}