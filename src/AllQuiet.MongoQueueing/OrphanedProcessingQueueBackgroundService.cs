using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AllQuiet.MongoQueueing;

public class OrphanedProcessingQueueBackgroundService<TPayload> : BackgroundService
{
    protected readonly ILogger<OrphanedProcessingQueueBackgroundService<TPayload>> logger;
    private readonly IQueue<TPayload> queue;

    public OrphanedProcessingQueueBackgroundService(ILogger<OrphanedProcessingQueueBackgroundService<TPayload>> logger, IQueue<TPayload> queue)
    {
        this.logger = logger;
        this.queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation($"OrphanedProcessingQueueBackgroundService for {typeof(TPayload).Name} started.");
                
        using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (
            !cancellationToken.IsCancellationRequested &&
            await timer.WaitForNextTickAsync(cancellationToken))
        {
            try 
            {

                await this.queue.EnqueueOrphanedProcessingAsync();
            } 
            catch (Exception ex)
            {
                logger.LogError(ex, $"EnqueueOrphanedProcessingAsync failed for {typeof(TPayload).Name}");
            }
        }
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"OrphanedProcessingQueueBackgroundService for {typeof(TPayload).Name} stopping ...");

        await base.StopAsync(cancellationToken);

        logger.LogInformation($"OrphanedProcessingQueueBackgroundService for {typeof(TPayload).Name} stopped.");
    }

}