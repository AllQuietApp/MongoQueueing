using AllQuiet.MongoQueueing.MongoDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllQuiet.MongoQueueing;

public class Queue<TPayload> : IQueue<TPayload>
{
    private readonly ILogger<Queue<TPayload>> logger;
    private readonly IQueuedItemRepository<TPayload> queuedItemRepository;
    private readonly QueueOptions queueOptions;

    public Queue(ILogger<Queue<TPayload>> logger, IQueuedItemRepository<TPayload> queuedItemRepository, IOptions<QueueOptions> queueOptions)
	{
		this.logger = logger;
		this.queuedItemRepository = queuedItemRepository;
		this.queueOptions = queueOptions.Value;
	}

	public async Task<QueuedItem<TPayload>> EnqueueAsync(TPayload payload, DateTime? nextReevaluation = null)
	{
		var queuedItem = new QueuedItem<TPayload>
		(
			new TimestampId(), 
			new [] { QueuedItemStatus.Enqueued(nextReevaluation) }, 
			payload
		);

		return await this.queuedItemRepository.InsertAsync(queuedItem);
	}


	private async Task<QueuedItem<TPayload>?> DequeueAsync(Func<TPayload, Task> processAsync, Func<Task<QueuedItem<TPayload>>> dequeueAsync)
	{
		var item = await dequeueAsync();
		if (item != null)
		{
			try 
			{
				await processAsync(item.Payload);
				await this.queuedItemRepository.UpdateStatusAsync(item.Id, QueuedItemStatus.Processed);
			} 
			catch(Exception ex)
			{
				this.logger.LogError(ex, $"Error processing item {item.Id} of queue {typeof(TPayload).Name}");
				
				var nextReevaluation = CalculateNextReevalation(item);

				await this.queuedItemRepository.UpdateStatusAsync(item.Id, 
					nextReevaluation != null ? QueuedItemStatus.Failed(nextReevaluation.Value) : QueuedItemStatus.FinallyFailed);
			}
		}
		return item;
	}

	private DateTime? CalculateNextReevalation(QueuedItem<TPayload> item)
	{
		var failedCount = item.Statuses.Count(status => status.Status == QueuedItemStatus.StatusFailed);
		if (failedCount > 6)
		{
			return null;
		}
		
		return DateTime.UtcNow.AddSeconds(failedCount * Math.Exp(failedCount * 2));
	}

	public async Task<QueuedItem<TPayload>?> DequeueAsync(Func<TPayload, Task> processAsync)
	{
		return await this.DequeueAsync(processAsync, async () => await this.queuedItemRepository.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusEnqueued, QueuedItemStatus.StatusProcessing, DateTime.UtcNow));
	}

    public async Task<QueuedItem<TPayload>?> DequeueFailedAsync(Func<TPayload, Task> processAsync)
    {
        return await this.DequeueAsync(processAsync, async () => await this.queuedItemRepository.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusFailed, QueuedItemStatus.StatusProcessing, DateTime.UtcNow));
    }

    public async Task<QueuedItem<TPayload>?> EnqueueOrphanedProcessingAsync()
    {
		return await this.queuedItemRepository.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusProcessing, QueuedItemStatus.StatusEnqueued, DateTime.UtcNow, System.DateTime.UtcNow.Add(this.queueOptions.ProcessingTimeout));
    }
}

public interface IQueue<TPayload>
{
	Task<QueuedItem<TPayload>> EnqueueAsync(TPayload payload, DateTime? nextReevaluation = null);
	Task<QueuedItem<TPayload>?> DequeueAsync(Func<TPayload, Task> processAsync);
    Task<QueuedItem<TPayload>?> DequeueFailedAsync(Func<TPayload, Task> processAsync);
    Task<QueuedItem<TPayload>?> EnqueueOrphanedProcessingAsync();
}

public record QueuedItem<TPayload>(TimestampId Id, IList<QueuedItemStatus> Statuses, TPayload Payload);

public record QueuedItemStatus(string Status, DateTime Timestamp, DateTime? NextReevaluation = null)
{
	public static QueuedItemStatus Enqueued() { return new QueuedItemStatus(StatusEnqueued, DateTime.UtcNow); }
	public static QueuedItemStatus Enqueued(DateTime? nextReevaluation = null) { return new QueuedItemStatus(StatusEnqueued, DateTime.UtcNow, nextReevaluation); }
	public static QueuedItemStatus Processing { get => new QueuedItemStatus(StatusProcessing, DateTime.UtcNow); }
	public static QueuedItemStatus Processed { get => new QueuedItemStatus(StatusProcessed, DateTime.UtcNow); }
	public static QueuedItemStatus Failed (DateTime nextReevaluation) => new QueuedItemStatus(StatusFailed, DateTime.UtcNow, nextReevaluation);
	public static QueuedItemStatus FinallyFailed { get => new QueuedItemStatus(StatusFinallyFailed, DateTime.UtcNow); }

	public static readonly string StatusEnqueued = "Enqueued";
	public static readonly string StatusProcessing = "Processing";
	public static readonly string StatusProcessed = "Processed";
	public static readonly string StatusFailed = "Failed";
	public static readonly string StatusFinallyFailed = "FinallyFailed";
}