using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AllQuiet.MongoQueueing;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds a dedicated queue for the payload <c>TPayload</c>. Payloads will be processed by <c>TProcessor</c>.
    /// </summary>
    /// <typeparam name="TPayload">The payload type</typeparam>
    /// <typeparam name="TProcessor">The type of your processor. Implements <c>IQueueProcessor{TPayload}</c></typeparam>
    public static void AddDedicatedQueueingFor<TPayload, TProcessor>(this IServiceCollection services)
        where TProcessor : class, IQueueProcessor<TPayload>
    {
        AddDedicatedQueueingFor<TPayload, TProcessor, QueuedItemRepository<TPayload>>(services);
    }

    /// <summary>
    /// Adds a dedicated queue for the payload <c>TPayload</c>. Payloads will be processed by <c>TProcessor</c>.
    /// </summary>
    /// <typeparam name="TPayload">The payload type</typeparam>
    /// <typeparam name="TProcessor">The type of your processor. Implements <c>IQueueProcessor{TPayload}</c></typeparam>
    /// <typeparam name="TQueuedItemRepository">For advanced use cases, you can specify your own <c>QueuedItemRepository{TPayload}</c> where queued items will be stored. 
    /// You want to specify your own QueuedItemRepository in cases when you want to access the current queue to e.g. show users how many items have been processed or are waiting in the queue.
    /// </typeparam>
    public static void AddDedicatedQueueingFor<TPayload, TProcessor, TQueuedItemRepository>(this IServiceCollection services)
        where TProcessor : class, IQueueProcessor<TPayload>
        where TQueuedItemRepository : QueuedItemRepository<TPayload>
    {
        services.AddScoped<IQueueProcessor<TPayload>, TProcessor>();
        services.AddSingleton<IQueuedItemRepository<TPayload>, TQueuedItemRepository>();
        
        services.AddSingleton<Queue<TPayload>>();
        services.AddSingleton<IQueue<TPayload>>(serviceProvider => serviceProvider.GetRequiredService<Queue<TPayload>>());
        services.AddSingleton<IDequeueableQueue<TPayload>>(serviceProvider => serviceProvider.GetRequiredService<Queue<TPayload>>());
        services.AddHostedService<QueueBackgroundService<TPayload>>(serviceProvider => {
            var options = serviceProvider.GetRequiredService<IOptions<QueueOptions>>();
            if (options.Value.UseChangeStream)
            {
                return new QueueChangeStreamBackgroundService<TPayload>(serviceProvider);
            }
            return new QueueBackgroundService<TPayload>(serviceProvider);
        });
        services.AddHostedService<FailedQueueBackgroundService<TPayload>>();
        services.AddHostedService<OrphanedProcessingQueueBackgroundService<TPayload>>();
    }

    /// <summary>
    /// Adds one generic queue. For each payload that you want to process on this generic queue, you should register one class that is derived from <c>GenericQueuePayloadProcessor{TPayload}</c>.
    /// </summary>
    public static void AddGenericQueueing(this IServiceCollection services)
    {
        AddDedicatedQueueingFor<GenericQueueEvent, GenericQueueProcessor>(services);
        services.AddSingleton<IGenericQueue, GenericQueue>();
    }
}
