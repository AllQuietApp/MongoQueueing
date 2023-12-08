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
        var newStatus = new QueuedItemStatus(statusAfterUpdate, DateTime.UtcNow);
        var update = Builders<QueuedItem<TPayload>>.Update.PushEach(status => status.Statuses, new [] { newStatus }, null, 0);

        if (statusTimestampBefore != null)
        {
            return await this.UpdateStatusAtomicallyByStatusAndStatusTimestampBeforeAsync(newStatus, update, statusBeforeUpdate, statusTimestampBefore);
        }

        if (queuedItemId != null)
        {
            return await this.UpdateStatusAtomicallyByStatusAndQueuedItemIdAsync(newStatus, update, statusBeforeUpdate, nextReevaluationBefore, queuedItemId.Value);
        }

        return await this.UpdateStatusAtomicallyByStatusAsync(newStatus, update, statusBeforeUpdate, nextReevaluationBefore);
    }

    private async Task<QueuedItem<TPayload>> UpdateStatusAtomicallyByStatusAsync(QueuedItemStatus newStatus, UpdateDefinition<QueuedItem<TPayload>> update, string statusBeforeUpdate, DateTime nextReevaluationBefore)
    {
        var filter = Builders<QueuedItem<TPayload>>.Filter.Or(
            Builders<QueuedItem<TPayload>>.Filter.And(
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                Builders<QueuedItem<TPayload>>.Filter.Lte("Statuses.0.NextReevaluation", nextReevaluationBefore)),
            Builders<QueuedItem<TPayload>>.Filter.And(
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.NextReevaluation", BsonNull.Value))
        );

        var hint = new BsonDocument(new Dictionary<string, object>
        {
            { "Statuses.0.Status",  1 },
            { "Statuses.0.NextReevaluation",  1 }
        });
        
        return await this.Collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<QueuedItem<TPayload>> { Hint = hint });    
    }

    private async Task<QueuedItem<TPayload>> UpdateStatusAtomicallyByStatusAndQueuedItemIdAsync(QueuedItemStatus newStatus, UpdateDefinition<QueuedItem<TPayload>> update, string statusBeforeUpdate, DateTime nextReevaluationBefore, TimestampId queuedItemId)
    {
        var filter = Builders<QueuedItem<TPayload>>.Filter.Or(
                Builders<QueuedItem<TPayload>>.Filter.And(
                    Builders<QueuedItem<TPayload>>.Filter.Eq("_id", queuedItemId),
                    Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                    Builders<QueuedItem<TPayload>>.Filter.Lte("Statuses.0.NextReevaluation", nextReevaluationBefore)),
                Builders<QueuedItem<TPayload>>.Filter.And(
                    Builders<QueuedItem<TPayload>>.Filter.Eq("_id", queuedItemId),
                    Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                    Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.NextReevaluation", BsonNull.Value)));
                    
        var hint = new BsonDocument(new Dictionary<string, object>
        {
            { "_id", 1 },
            { "Statuses.0.Status",  1 },
            { "Statuses.0.NextReevaluation",  1 }
        });
        
        return await this.Collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<QueuedItem<TPayload>> { Hint = hint });    
    }

    private async Task<QueuedItem<TPayload>> UpdateStatusAtomicallyByStatusAndStatusTimestampBeforeAsync(QueuedItemStatus newStatus, UpdateDefinition<QueuedItem<TPayload>> update, string statusBeforeUpdate, DateTime? statusTimestampBefore)
    {
        var filter = Builders<QueuedItem<TPayload>>.Filter.And(
                Builders<QueuedItem<TPayload>>.Filter.Eq("Statuses.0.Status", statusBeforeUpdate),
                Builders<QueuedItem<TPayload>>.Filter.Lte("Statuses.0.Timestamp", statusTimestampBefore));

        var hint = new BsonDocument(new Dictionary<string, object>
        {
            { "Statuses.0.Status",  1 },
            { "Statuses.0.Timestamp",  1 }
        });
        
        return await this.Collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<QueuedItem<TPayload>> { Hint = hint });              
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
            new CreateIndexModel<QueuedItem<TPayload>>(Builders<QueuedItem<TPayload>>.IndexKeys.Ascending(x => x.Id).Ascending(x => x.Statuses[0].Status).Ascending(x => x.Statuses[0].NextReevaluation)),
            new CreateIndexModel<QueuedItem<TPayload>>(Builders<QueuedItem<TPayload>>.IndexKeys.Ascending(x => x.Statuses[0].Status).Ascending(x => x.Statuses[0].NextReevaluation)),
            new CreateIndexModel<QueuedItem<TPayload>>(Builders<QueuedItem<TPayload>>.IndexKeys.Ascending(x => x.Statuses[0].Status).Ascending(x => x.Statuses[0].Timestamp))
        };
    }

    public async Task DeleteAsync(TimestampId id)
    {
       await this.Collection.DeleteOneAsync(Builders<QueuedItem<TPayload>>.Filter.Eq(item => item.Id, id));
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
    Task DeleteAsync(TimestampId id);

    IMongoCollection<QueuedItem<TPayload>> Collection { get; }
}
