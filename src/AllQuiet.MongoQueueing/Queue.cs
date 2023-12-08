using AllQuiet.MongoQueueing.MongoDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing;

public class Queue<TPayload> : IDequeueableQueue<TPayload>, IQueue<TPayload>
{
    private readonly ILogger<Queue<TPayload>> logger;
    private readonly IQueuedItemRepository<TPayload> queuedItemRepository;
    private readonly QueueOptions queueOptions;
	
    public Queue(ILogger<Queue<TPayload>> logger, IQueuedItemRepository<TPayload> queuedItemRepository, IOptions<QueueOptions> queueOptions)
	{
		if (queueOptions.Value.ProcessingTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(queueOptions.Value.ProcessingTimeout));
		}

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
				if (this.queueOptions.ClearSuccessfulMessages)
				{
					await this.queuedItemRepository.DeleteAsync(item.Id);
				}
				else
				{
					
					await this.queuedItemRepository.UpdateStatusAsync(item.Id, QueuedItemStatus.Processed);
				}
			} 
			catch(Exception ex)
			{
				this.logger.LogError(ex, $"Error processing item {item.Id} of queue {typeof(TPayload).Name}");
				
				var nextReevaluation = CalculateNextReevalation(item);

				await this.queuedItemRepository.UpdateStatusAsync(item.Id, 
					nextReevaluation != null ? QueuedItemStatus.Failed(nextReevaluation.Value, queueOptions.PersistException ? ex : null) : QueuedItemStatus.FinallyFailed);
			}
		}
		return item;
	}

	private DateTime? CalculateNextReevalation(QueuedItem<TPayload> item)
	{
		var failedCount = item.Statuses.Count(status => status.Status == QueuedItemStatus.StatusFailed);
		if (failedCount > this.queueOptions.RetryIntervalsInSeconds.Length - 1)
		{
			return null;
		}
		
		return item.Statuses[0].Timestamp.AddSeconds(this.queueOptions.RetryIntervalsInSeconds[failedCount]);
	}

	public async Task<QueuedItem<TPayload>?> DequeueAsync(TimestampId? queuedItemId, Func<TPayload, Task> processAsync)
	{
		return await this.DequeueAsync(
			processAsync, 
			
			async () => await this.queuedItemRepository.FindOneByStatusAndUpdateStatusAtomicallyAsync(
			QueuedItemStatus.StatusEnqueued, QueuedItemStatus.StatusProcessing, DateTime.UtcNow, null, queuedItemId));
	}

    public async Task<QueuedItem<TPayload>?> DequeueFailedAsync(Func<TPayload, Task> processAsync)
    {
        return await this.DequeueAsync(processAsync, async () => await this.queuedItemRepository.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusFailed, QueuedItemStatus.StatusProcessing, DateTime.UtcNow));
    }

    public async Task<QueuedItem<TPayload>?> EnqueueOrphanedProcessingAsync()
    {
		return await this.queuedItemRepository.FindOneByStatusAndUpdateStatusAtomicallyAsync(
			QueuedItemStatus.StatusProcessing, 
			QueuedItemStatus.StatusEnqueued, 
			DateTime.UtcNow, 
			System.DateTime.UtcNow.Add(-this.queueOptions.ProcessingTimeout));
    }

    public async Task<IChangeStreamCursor<ChangeStreamDocument<QueuedItem<TPayload>>>> CreateInsertedChangeStreamAsync(CancellationToken cancellationToken)
    {
		var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<AllQuiet.MongoQueueing.QueuedItem<TPayload>>>().Match(x => x.OperationType == ChangeStreamOperationType.Insert);
		return await this.queuedItemRepository.Collection.WatchAsync(pipeline, null, cancellationToken);
    }
}

public interface IDequeueableQueue<TPayload> : IQueue<TPayload>
{
	Task<QueuedItem<TPayload>?> DequeueAsync(TimestampId? queuedItemId, Func<TPayload, Task> processAsync);
    Task<QueuedItem<TPayload>?> DequeueFailedAsync(Func<TPayload, Task> processAsync);
    Task<QueuedItem<TPayload>?> EnqueueOrphanedProcessingAsync();
    Task<IChangeStreamCursor<ChangeStreamDocument<QueuedItem<TPayload>>>> CreateInsertedChangeStreamAsync(CancellationToken cancellationToken);
}

public interface IQueue<TPayload>
{
    /// <summary>
    /// Asynchronously enqueues a new payload into the current queue.
    /// </summary>
    /// <param name="payload">The payload to be enqueued.</param>
    /// <param name="nextReevaluation">
    /// Optional: Specifies the earliest time the payload should be processed. 
    /// If null, the payload is processed as soon as possible. 
    /// If provided, it schedules the payload for future processing at the specified DateTime or later.
    /// </param>
    /// <returns>
    /// A task that, when completed, returns the queued item containing the enqueued payload.
    /// </returns>
    /// <remarks>
    /// This method allows for both immediate and scheduled processing of payloads.
    /// Scheduling is useful for deferred processing or implementing delay/retry mechanisms.
    /// </remarks>
	Task<QueuedItem<TPayload>> EnqueueAsync(TPayload payload, DateTime? nextReevaluation = null);
}

public record QueuedItem<TPayload>(TimestampId Id, IList<QueuedItemStatus> Statuses, TPayload Payload);

public record QueuedItemStatus(string Status, DateTime Timestamp, DateTime? NextReevaluation = null, QueuedItemStatusException? Exception = null)
{
	public static QueuedItemStatus Enqueued() { return new QueuedItemStatus(StatusEnqueued, DateTime.UtcNow); }
	public static QueuedItemStatus Enqueued(DateTime? nextReevaluation = null) { return new QueuedItemStatus(StatusEnqueued, DateTime.UtcNow, nextReevaluation); }
	public static QueuedItemStatus Processing { get => new QueuedItemStatus(StatusProcessing, DateTime.UtcNow); }
	public static QueuedItemStatus Processed { get => new QueuedItemStatus(StatusProcessed, DateTime.UtcNow); }
	public static QueuedItemStatus Failed (DateTime nextReevaluation, Exception? exception) => new QueuedItemStatus(StatusFailed, DateTime.UtcNow, nextReevaluation, exception != null ? new QueuedItemStatusException(exception) : null);
	public static QueuedItemStatus FinallyFailed { get => new QueuedItemStatus(StatusFinallyFailed, DateTime.UtcNow); }

	public static readonly string StatusEnqueued = "Enqueued";
	public static readonly string StatusProcessing = "Processing";
	public static readonly string StatusProcessed = "Processed";
	public static readonly string StatusFailed = "Failed";
	public static readonly string StatusFinallyFailed = "FinallyFailed";
}

public record QueuedItemStatusException(string Message, string? StackTrace)
{
	public QueuedItemStatusException(Exception exception) : this(exception.Message, exception.StackTrace)
	{

	}
}