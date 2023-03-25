namespace AllQuiet.MongoQueueing;

public class QueueOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan FailedPollInterval { get; set; } = TimeSpan.FromMilliseconds(1000);
    public TimeSpan OrphanedPollInterval { get; set; } = TimeSpan.FromSeconds(10);
}
   