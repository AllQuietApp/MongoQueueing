namespace AllQuiet.MongoQueueing;

public class QueueOptions
{
    /// <summary>
    ///  How often to poll for new payloads in the queue. Default 100ms.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///  How often to poll for failed payloads in the queue. Default 1s.
    /// </summary>
    public TimeSpan FailedPollInterval { get; set; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// How often to poll timed out payloads in the queue. Default 10s.
    /// </summary>
    public TimeSpan OrphanedPollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// After what time should a payload which is in status "processing" considered as timed out and will be restarted. Default 30min.
    /// </summary>
    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(30);    
}
   