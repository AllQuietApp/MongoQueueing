using Microsoft.Extensions.Logging;

namespace AllQuiet.MongoQueueing
{
    public class GenericQueueProcessor : IQueueProcessor<GenericQueueEvent>
    {
        private readonly ILogger<GenericQueueProcessor> logger;
        private readonly IEnumerable<IGenericQueuePayloadProcessor> payloadProcessors;

        public GenericQueueProcessor(
            ILogger<GenericQueueProcessor> logger,
            IEnumerable<IGenericQueuePayloadProcessor> payloadProcessors
        )
        {
            this.logger = logger;
            this.payloadProcessors = payloadProcessors;
        }

        public async Task ProcessAsync(GenericQueueEvent genericQueueEvent)
        {
            if (genericQueueEvent.Payload == null)
            {
                throw new ArgumentNullException(nameof(genericQueueEvent.Payload));
            }

            this.logger.LogInformation($"ProcessQueuedItemAsync for genericQueueEvent {genericQueueEvent.Payload.GetType()}");

            foreach (var payloadProcessor in payloadProcessors)
            {
                if (payloadProcessor.CanProcess(genericQueueEvent.Payload))
                {
                    
                    this.logger.LogInformation($"Found processor {payloadProcessor.GetType()}");
                    await payloadProcessor.ProcessAsync(genericQueueEvent.Payload);
                    return;
                }
            }
            
            throw new Exception($"No processor found for {genericQueueEvent.Payload.GetType()}");
        }
    }
}