using Microsoft.Extensions.DependencyInjection;

namespace AllQuiet.MongoQueueing;

public static class IServiceCollectionExtensions
{
    public static void AddQueueingFor<TPayload, TProcessor>(this IServiceCollection services)
        where TProcessor : class, IQueueProcessor<TPayload>
    {
        AddQueueingFor<TPayload, TProcessor, QueuedItemRepository<TPayload>>(services);
    }

    public static void AddQueueingFor<TPayload, TProcessor, TQueuedItemRepository>(this IServiceCollection services)
        where TProcessor : class, IQueueProcessor<TPayload>
        where TQueuedItemRepository : QueuedItemRepository<TPayload>
    {
        services.AddScoped<IQueueProcessor<TPayload>, TProcessor>();
        services.AddSingleton<IQueuedItemRepository<TPayload>, TQueuedItemRepository>();
        services.AddSingleton<AllQuiet.MongoQueueing.IQueue<TPayload>, AllQuiet.MongoQueueing.Queue<TPayload>>();
        services.AddHostedService<QueueBackgroundService<TPayload>>();
        services.AddHostedService<FailedQueueBackgroundService<TPayload>>();
        services.AddHostedService<OrphanedProcessingQueueBackgroundService<TPayload>>();
    }

    public static void AddGenericQueueing(this IServiceCollection services)
    {
        AddQueueingFor<GenericQueueEvent, GenericQueueProcessor>(services);
        services.AddSingleton<IGenericQueue, GenericQueue>();
    }
}
