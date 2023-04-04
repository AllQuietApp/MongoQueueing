namespace AllQuiet.MongoQueueing;

public class QueueOptions
{

    /// <summary>
    ///  How often to poll for new payloads in the queue. Default 1s.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///  How often to poll for failed payloads in the queue. Default 10s.
    /// </summary>
    public TimeSpan FailedPollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often to poll timed out payloads in the queue. Default 1min.
    /// </summary>
    public TimeSpan OrphanedPollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// After what time should a payload which is in status "processing" considered as timed out and will be restarted. Default 30min.
    /// </summary>
    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Wether to use polling or MongoDB change streams. Change streams are only supported in replica sets.
    /// </summary>
    public bool UseChangeStream { get; set; } = false;
}