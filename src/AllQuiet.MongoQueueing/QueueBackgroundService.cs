using AllQuiet.MongoQueueing.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllQuiet.MongoQueueing;

public class QueueBackgroundService<TPayload> : BackgroundService
{
    protected readonly IServiceProvider serviceProvider;
    protected readonly ILogger<QueueBackgroundService<TPayload>> logger;
    protected readonly IDequeueableQueue<TPayload> queue;
    protected readonly QueueOptions options;

    public QueueBackgroundService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        this.logger = this.serviceProvider.GetRequiredService<ILogger<QueueBackgroundService<TPayload>>>();
        this.queue = this.serviceProvider.GetRequiredService<IDequeueableQueue<TPayload>>();
        this.options = this.serviceProvider.GetRequiredService<IOptions<QueueOptions>>().Value;
    }

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation($"{this.GetType().Name} for {typeof(TPayload).Name} starting at interval {this.options.PollInterval}...");
        this.InitializeServices();
        this.logger.LogInformation($"{this.GetType().Name} for {typeof(TPayload).Name} started.");
        await StartPolling(cancellationToken);
    }

    protected async Task StartPolling(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(options.PollInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            var item = await this.DequeueAsync(null);

            if (item == null)    
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
    }

    protected void InitializeServices()
    {
        this.logger.LogDebug($"InitializeServices for {typeof(TPayload).Name} started.");
        try
        {
            using (var scope = this.serviceProvider.CreateScope())
            {
                this.logger.LogDebug($"CreateScope done. Getting required Service...");
                scope.ServiceProvider.GetRequiredService<IQueueProcessor<TPayload>>();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"InitializeServices failed for {typeof(TPayload).Name}");
        }
        
        this.logger.LogDebug($"InitializeServices for {typeof(TPayload).Name} finished.");
    }

    protected async Task<QueuedItem<TPayload>?> DequeueAsync(TimestampId? queuedItemId)
    {
        try 
        {
            return await this.queue.DequeueAsync(queuedItemId, async payload => {
                using (var scope = this.serviceProvider.CreateScope())
                {
                    var queueProcessor = scope.ServiceProvider.GetRequiredService<IQueueProcessor<TPayload>>();
                    await queueProcessor.ProcessAsync(payload);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"DequeueAsync failed for {typeof(TPayload).Name}");
        }
        return null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug($"{this.GetType().Name} for {typeof(TPayload).Name} stopping ...");

        await base.StopAsync(cancellationToken);

        logger.LogDebug($"{this.GetType().Name} for {typeof(TPayload).Name} stopped.");
    }

}
