using AllQuiet.MongoQueueing.MongoDB;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing;

public class QueuedItemRepository<TPayload> : MongoRepository<QueuedItem<TPayload>>, IQueuedItemRepository<TPayload>
{
    public QueuedItemRepository(ILogger<QueuedItemRepository<TPayload>> logger, IMongoDatabase database, string? collectionName = null) : 
        base(logger, database, collectionName ?? $"queueItems{typeof(TPayload).Name}")
    {
    }

    public async Task<QueuedItem<TPayload>> InsertAsync(QueuedItem<TPayload> queuedItem)
    {
        return await this.InsertWithUniqueTimestampId(id => new QueuedItem<TPayload>(id, queuedItem.Statuses, queuedItem.Payload));
    }

    public async Task<QueuedItem<TPayload>> FindOneByStatusAndUpdateStatusAtomicallyAsync(
        string statusBeforeUpdate, string statusAfterUpdate, DateTime nextReevaluationBefore, DateTime? statusTimestampBefore = null, TimestampId? queuedItemId = null)
    {
        var filter = GetFilterFindOneByStatusAndUpdateStatusAtomicallyAsync(statusBeforeUpdate, nextReevaluationBefore, statusTimestampBefore);
        if (queuedItemId != null)
        {
            filter = Builders<QueuedItem<TPayload>>.Filter.And(Builders<QueuedItem<TPayload>>.Filter.Eq(item => item.Id, queuedItemId.Value), filter);
        }
        var newStatus = new QueuedItemStatus(statusAfterUpdate, DateTime.UtcNow);
        var update = Builders<QueuedItem<TPayload>>.Update.PushEach(status => status.Statuses, new [] { newStatus }, null, 0);
        return await this.Collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<QueuedItem<TPayload>>());
    }

    private FilterDefinition<QueuedItem<TPayload>> GetFilterFindOneByStatusAndUpdateStatusAtomicallyAsync(string statusBeforeUpdate, DateTime nextReevaluationBefore, DateTime? statusTimestampBefore)
    {
        if (statusTimestampBefore != null)
        {
            return Builders<QueuedItem<TPayload>>.Filter.And(
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                Builders<QueuedItem<TPayload>>.Filter.Lte("Statuses.0.Timestamp", statusTimestampBefore));
        }

        return Builders<QueuedItem<TPayload>>.Filter.Or(
            Builders<QueuedItem<TPayload>>.Filter.And(
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                Builders<QueuedItem<TPayload>>.Filter.Lte("Statuses.0.NextReevaluation", nextReevaluationBefore)),
            Builders<QueuedItem<TPayload>>.Filter.And(
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.NextReevaluation", BsonNull.Value))
        );
    }

    public async Task UpdateStatusAsync(
        TimestampId id, QueuedItemStatus statusAfterUpdate)
    {
        var update = Builders<QueuedItem<TPayload>>.Update.PushEach(status => status.Statuses, new [] { statusAfterUpdate }, null, 0);
        var filter = Builders<QueuedItem<TPayload>>.Filter.Eq(item => item.Id, id);
        await this.Collection.UpdateOneAsync(filter, update);
    }


    protected override IEnumerable<CreateIndexModel<QueuedItem<TPayload>>> GetIndexModels()
    {
        return new [] 
        {
            //new CreateIndexModel<QueuedItem<TPayload>>(Builders<QueuedItem<TPayload>>.IndexKeys.Ascending(x => x.Id).Ascending(x => x.Statuses[0].Status).Ascending(x => x.Statuses[0].NextReevaluation)),
            new CreateIndexModel<QueuedItem<TPayload>>(Builders<QueuedItem<TPayload>>.IndexKeys.Ascending(x => x.Statuses[0].Status).Ascending(x => x.Statuses[0].NextReevaluation)),
            new CreateIndexModel<QueuedItem<TPayload>>(Builders<QueuedItem<TPayload>>.IndexKeys.Ascending(x => x.Statuses[0].NextReevaluation))
        };
    }

    public new IMongoCollection<QueuedItem<TPayload>> Collection => base.Collection;
}

public interface IQueuedItemRepository<TPayload>
{
    Task<QueuedItem<TPayload>> InsertAsync(QueuedItem<TPayload> queuedItem);
    Task<QueuedItem<TPayload>> FindOneByStatusAndUpdateStatusAtomicallyAsync(
        string statusBeforeUpdate, string statusAfterUpdate, DateTime nextReevaluationBefore, DateTime? statusTimestampBefore = null, TimestampId? queuedItemId = null);
    Task UpdateStatusAsync(
        TimestampId id, QueuedItemStatus statusAfterUpdate);
    IMongoCollection<QueuedItem<TPayload>> Collection { get; }
}
