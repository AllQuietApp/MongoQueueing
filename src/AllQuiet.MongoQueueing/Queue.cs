using AllQuiet.MongoQueueing.MongoDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing;

public class Queue<TPayload> : IDequeueableQueue<TPayload>, IQueue<TPayload>
{
    private readonly ILogger<Queue<TPayload>> logger;
    private readonly IQueuedItemRepository<TPayload> queuedItemRepository;
    private readonly QueueOptions queueOptions;
	private static readonly int[] RetryIntervalsInSeconds = new []
	{
		1,
		2,
		10,
		30,
		60,
		3600,
	};

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
		if (failedCount > RetryIntervalsInSeconds.Length - 1)
		{
			return null;
		}
		
		return item.Statuses[0].Timestamp.AddSeconds(RetryIntervalsInSeconds[failedCount]);
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
    /// Enqueues a new payload on the current queue.
	/// </summary>
	/// <param name="payload">The payload to enqueue.</param>
	/// <param name="nextReevaluation">If null, then the payload is processed as soon as possible. If specified, the payload will be processed at earliest after the provided DateTime. Use this param to schedule processing in the future.</param>
	/// <returns>The queued item with the attached payload.</returns>
	Task<QueuedItem<TPayload>> EnqueueAsync(TPayload payload, DateTime? nextReevaluation = null);
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