namespace AllQuiet.MongoQueueing
{
    /// <summary>
    /// Implement this interface in the dedicated processor for payloads of type <c>TPayload</c>.
    /// </summary>
    /// <typeparam name="TPayload"></typeparam>
    public interface IQueueProcessor<TPayload>
    {
        /// <summary>
        /// Processes the payload. When the functions is executed successfully without throwing an exception, the payload is marked as successfully <c>processed</c>.
        /// If an exception is thrown, the payload will be marked as <c>failed</c> and execution will be retried.
        /// </summary>
        /// <param name="payload">The payload which was dequeued.</param>
        Task ProcessAsync(TPayload payload);
    }
}