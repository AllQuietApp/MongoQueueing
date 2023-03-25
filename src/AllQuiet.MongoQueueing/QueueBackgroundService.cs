using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllQuiet.MongoQueueing;

public class QueueBackgroundService<TPayload> : BackgroundService
{
    protected readonly IServiceProvider serviceProvider;
    protected readonly ILogger<QueueBackgroundService<TPayload>> logger;
    private readonly IQueue<TPayload> queue;
    private readonly QueueOptions options;

    public QueueBackgroundService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        this.logger = this.serviceProvider.GetRequiredService<ILogger<QueueBackgroundService<TPayload>>>();
        this.queue = this.serviceProvider.GetRequiredService<IQueue<TPayload>>();
        this.options = this.serviceProvider.GetRequiredService<IOptions<QueueOptions>>().Value;
    }

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation($"QueueBackgroundService for {typeof(TPayload).Name} starting at interval {this.options.ServiceQueueInterval}...");
        InitializeServices();
        this.logger.LogInformation($"QueueBackgroundService for {typeof(TPayload).Name} started.");
        await StartDequeueing(cancellationToken);
    }

    private async Task StartDequeueing(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(options.ServiceQueueInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            var item = await this.DequeueAsync();

            if (item == null)    
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
    }

    private void InitializeServices()
    {
        this.logger.LogInformation($"InitializeServices for {typeof(TPayload).Name} started.");
        try
        {
            using (var scope = this.serviceProvider.CreateScope())
            {
                this.logger.LogInformation($"CreateScope done. Getting required Service...");
                scope.ServiceProvider.GetRequiredService<IQueueProcessor<TPayload>>();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"InitializeServices failed for {typeof(TPayload).Name}");
        }
        
        this.logger.LogInformation($"InitializeServices for {typeof(TPayload).Name} finished.");
    }

    private async Task<QueuedItem<TPayload>?> DequeueAsync()
    {
        try 
        {
            return await this.queue.DequeueAsync(async payload => {
                using (var scope = this.serviceProvider.CreateScope())
                {
                    var queueProcessor = scope.ServiceProvider.GetRequiredService<IQueueProcessor<TPayload>>();
                    await queueProcessor.ProcessQueuedItemAsync(payload);
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
        logger.LogInformation($"QueueBackgroundService for {typeof(TPayload).Name} stopping ...");

        await base.StopAsync(cancellationToken);

        logger.LogInformation($"QueueBackgroundService for {typeof(TPayload).Name} stopped.");
    }

}
