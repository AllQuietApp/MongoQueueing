using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllQuiet.MongoQueueing;

public class FailedQueueBackgroundService<TPayload> : BackgroundService
{
    protected readonly IServiceProvider serviceProvider;
    protected readonly ILogger<FailedQueueBackgroundService<TPayload>> logger;
    private readonly IDequeueableQueue<TPayload> queue;
    private readonly QueueOptions options;

    public FailedQueueBackgroundService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        this.logger = this.serviceProvider.GetRequiredService<ILogger<FailedQueueBackgroundService<TPayload>>>();
        this.queue = this.serviceProvider.GetRequiredService<IDequeueableQueue<TPayload>>();
        this.options = this.serviceProvider.GetRequiredService<IOptions<QueueOptions>>().Value;
    }

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation($"FailedQueueBackgroundService for {typeof(TPayload).Name} started.");
        InitializeServices();
        await StartDequeueing(cancellationToken);
    }

    private void InitializeServices()
    {
        using (var scope = this.serviceProvider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IQueueProcessor<TPayload>>();
        }
    }

    private async Task StartDequeueing(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(this.options.FailedPollInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            var item = await this.DequeueFailedAsync();

            if (item == null)    
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
    }

    private async Task<QueuedItem<TPayload>?> DequeueFailedAsync()
    {
        try
        {
            return await this.queue.DequeueFailedAsync(async payload => {
                using (var scope = this.serviceProvider.CreateScope())
                {
                    var queueProcessor = scope.ServiceProvider.GetRequiredService<IQueueProcessor<TPayload>>();
                    await queueProcessor.ProcessAsync(payload);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"DequeueFailedAsync failed for {typeof(TPayload).Name}.");
        }
        return null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug($"FailedQueueBackgroundService for {typeof(TPayload).Name} stopping ...");

        await base.StopAsync(cancellationToken);

        logger.LogDebug($"FailedQueueBackgroundService for {typeof(TPayload).Name} stopped.");
    }

}