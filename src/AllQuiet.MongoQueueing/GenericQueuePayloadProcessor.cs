using MongoDB.Bson.Serialization;

namespace AllQuiet.MongoQueueing
{
    /// <summary>
    /// Derive from this class to process payloads that are enqueued on the generic queue.
    /// </summary>
    /// <typeparam name="TPayload">The payload type that you want to process.</typeparam>
    public abstract class GenericQueuePayloadProcessor<TPayload> : IGenericQueuePayloadProcessor
    {
        private readonly Type payloadType;

        static GenericQueuePayloadProcessor()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(TPayload)))
            {
                BsonClassMap.RegisterClassMap<TPayload>();
            }
        }

        public GenericQueuePayloadProcessor()
        {
            this.payloadType = typeof(TPayload);
        }

        public bool CanProcess(object payload)
        {
            return this.payloadType == payload.GetType();
        }

        public async Task ProcessAsync(object payload)
        {
            await this.ProcessAsync((TPayload)payload);
        }

        /// <summary>
        /// Should process the payload. When the functions is executed successfully without throwing an exception, the payload is marked as successfully <c>processed</c>.
        /// If an exception is thrown, the payload will be marked as <c>failed</c> and execution will be retried.
        /// </summary>
        /// <param name="payload">The payload which was dequeued.</param>
        protected abstract Task ProcessAsync(TPayload payload);
    }

    public interface IGenericQueuePayloadProcessor
    {
        bool CanProcess(object payload);
        Task ProcessAsync(object payload);
    }
}