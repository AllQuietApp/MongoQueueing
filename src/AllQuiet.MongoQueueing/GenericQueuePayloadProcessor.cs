using MongoDB.Bson.Serialization;

namespace AllQuiet.MongoQueueing
{
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

        protected abstract Task ProcessAsync(TPayload payload);
    }

    public interface IGenericQueuePayloadProcessor
    {
        bool CanProcess(object payload);
        Task ProcessAsync(object payload);
    }
}