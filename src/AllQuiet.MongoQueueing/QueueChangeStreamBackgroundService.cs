using AllQuiet.MongoQueueing.MongoDB;
using Microsoft.Extensions.Logging;

namespace AllQuiet.MongoQueueing;

public class QueueChangeStreamBackgroundService<TPayload> : QueueBackgroundService<TPayload>
{

    public QueueChangeStreamBackgroundService(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation($"QueueChangeStreamBackgroundService for {typeof(TPayload).Name} starting at interval {this.options.PollInterval}...");
        InitializeServices();
        this.logger.LogInformation($"QueueChangeStreamBackgroundService for {typeof(TPayload).Name} started.");
        
        var watchChangeStreamTask = WatchChangeStream(cancellationToken);
        var startPollingTask = StartPolling(cancellationToken);
        await Task.WhenAll(watchChangeStreamTask, startPollingTask);
    }

    private async Task WatchChangeStream(CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            try 
            {
                using (var cursor = await this.queue.CreateInsertedChangeStreamAsync(cancellationToken))
                {
                    while (!cancellationToken.IsCancellationRequested && await cursor.MoveNextAsync(cancellationToken))
                    {
                        foreach (var change in cursor.Current)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }
                            await this.DequeueAsync(new TimestampId((ulong)change.DocumentKey["_id"].AsInt64));
                        }
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex, $"Error reading from change stream for {typeof(TPayload).Name}.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error reading from change stream for {typeof(TPayload).Name}.");
                await Task.Delay(500);
            }
        }
    }
}
