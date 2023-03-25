namespace AllQuiet.MongoQueueing
{
    public interface IQueueProcessor<TPayload>
    {
        Task ProcessQueuedItemAsync(TPayload payload);
    }
}