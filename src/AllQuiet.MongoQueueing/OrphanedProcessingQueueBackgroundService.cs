using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllQuiet.MongoQueueing;

public class OrphanedProcessingQueueBackgroundService<TPayload> : BackgroundService
{
    protected readonly ILogger<OrphanedProcessingQueueBackgroundService<TPayload>> logger;
    private readonly IDequeueableQueue<TPayload> queue;
    private readonly QueueOptions options;

    public OrphanedProcessingQueueBackgroundService(ILogger<OrphanedProcessingQueueBackgroundService<TPayload>> logger, IDequeueableQueue<TPayload> queue, IOptions<QueueOptions> options)
    {
        this.logger = logger;
        this.queue = queue;
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation($"OrphanedProcessingQueueBackgroundService for {typeof(TPayload).Name} started.");

        using PeriodicTimer timer = new PeriodicTimer(options.OrphanedPollInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            var orphanedItem = await this.DequeueOrphanedItemAsync();
            if (orphanedItem == null)
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
    }

    private async Task<QueuedItem<TPayload>?> DequeueOrphanedItemAsync()
    {               
        try 
        {
            this.logger.LogInformation($"DequeueOrphanedItemAsync for {typeof(TPayload).Name} executing.");
            return await this.queue.EnqueueOrphanedProcessingAsync();
        } 
        catch (Exception ex)
        {
            logger.LogError(ex, $"DequeueOrphanedItemAsync failed for {typeof(TPayload).Name}");
        }
        return null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"OrphanedProcessingQueueBackgroundService for {typeof(TPayload).Name} stopping ...");

        await base.StopAsync(cancellationToken);

        logger.LogInformation($"OrphanedProcessingQueueBackgroundService for {typeof(TPayload).Name} stopped.");
    }

}