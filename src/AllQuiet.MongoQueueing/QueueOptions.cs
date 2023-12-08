namespace AllQuiet.MongoQueueing;

public class QueueOptions
{

    /// <summary>
    /// Specifies the frequency of polling for new payloads in the queue.
    /// </summary>
    /// <remarks>
    /// A shorter interval leads to quicker detection of new payloads but may increase system load.
    /// The default interval is set to 1 second.
    /// </remarks>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Determines how frequently the system checks for failed payloads in the queue.
    /// </summary>
    /// <remarks>
    /// Adjusting this value can help manage how often failed payloads are retried or inspected.
    /// Default value is 10 seconds.
    /// </remarks>
    public TimeSpan FailedPollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Sets the interval for polling payloads that have timed out in the queue.
    /// </summary>
    /// <remarks>
    /// This interval helps in identifying and handling payloads that have not been processed within their expected time frame.
    /// Default setting is 1 minute.
    /// </remarks>
    public TimeSpan OrphanedPollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Defines the timeout duration for payloads in a 'processing' state before they are considered timed out.
    /// </summary>
    /// <remarks>
    /// This setting is crucial for handling cases where a payload may be stuck or processing longer than expected.
    /// The default timeout is 30 minutes.
    /// </remarks>
    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Indicates whether to use polling or MongoDB change streams for queue updates.
    /// </summary>
    /// <remarks>
    /// Change streams provide a more efficient way to receive updates, but they require a MongoDB replica set.
    /// By default, this is set to false, using polling.
    /// </remarks>
    public bool UseChangeStream { get; set; } = false;

    /// <summary>
    /// Specifies the intervals for retrying failed payload processing, in seconds.
    /// </summary>
    /// <remarks>
    /// Each element in the array represents the time to wait before the next retry attempt.
    /// The length of the array also determines the total number of retry attempts.
    /// </remarks>
    public int[] RetryIntervalsInSeconds { get; set; } = new []
	{
		1,
		2,
		10,
		30,
		60,
		3600,
	};

    /// <summary>
    /// Indicates whether exceptions should be persisted for analysis.
    /// </summary>
    /// <remarks>
    /// When set to true, exceptions that occur during queue processing will be stored for further investigation.
    /// Default value is false, meaning exceptions are not persisted by default.
    /// </remarks>
    public bool PersistException { get; set; } = false;

    /// <summary>
    /// Indicates whether successfully processed messages should be deleted from the queue collection.
    /// </summary>
    /// <remarks>
    /// When set to true, messages that have been successfully processed will be removed from the queue.
    /// This can help in maintaining a cleaner queue and reducing storage usage.
    /// The default value is false, meaning successfully processed messages are not cleared by default.
    /// </remarks>
    public bool ClearSuccessfulMessages { get; set; } = false;
}