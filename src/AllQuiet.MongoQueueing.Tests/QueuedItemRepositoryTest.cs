using AllQuiet.MongoQueueing.MongoDB;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.Tests.Queuing;

public class QueuedItemRepositoryTest : MongoDBTest
{
    private readonly QueuedItemRepository<SomePayload> sut;
    private readonly IMongoCollection<QueuedItem<SomePayload>> collection;

    public QueuedItemRepositoryTest()
    {
        var collectionName = "queuedItemsSomePayload";
        this.sut = new QueuedItemRepository<SomePayload>(A.Dummy<ILogger<QueuedItemRepository<SomePayload>>>(), this.MongoDatabase, collectionName);
        this.collection = this.MongoDatabase.GetCollection<QueuedItem<SomePayload>>(collectionName);
    }

    [Fact]
    public async Task FindOneByStatusAndUpdateStatusAtomicallyAsync()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var queuedItem = await this.sut.InsertAsync(new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { QueuedItemStatus.Enqueued() },
            new SomePayload()
        ));

        // Act & Assert
        var itemEnqueued = await this.sut.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusEnqueued, QueuedItemStatus.StatusProcessing, now);

        Assert.NotNull(itemEnqueued);
        Assert.NotNull(itemEnqueued.Statuses);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, itemEnqueued.Statuses[0].Status);

        var itemProcessing = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();

        Assert.NotNull(itemProcessing);
        Assert.NotNull(itemProcessing.Statuses);
        Assert.Equal(QueuedItemStatus.StatusProcessing, itemProcessing.Statuses[0].Status);

        await this.sut.UpdateStatusAsync(queuedItem.Id, QueuedItemStatus.Processed);
        var itemProcessed = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();

        Assert.NotNull(itemProcessed);
        Assert.NotNull(itemProcessed.Statuses);
        Assert.Equal(QueuedItemStatus.StatusProcessed, itemProcessed.Statuses[0].Status);
    }

    [Fact]
    public async Task FindOneByStatusAndUpdateStatusAtomicallyWithNextReevaluationAsync()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var queuedItem = await this.sut.InsertAsync(new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { QueuedItemStatus.Enqueued(now.AddMinutes(1)) },
            new SomePayload()
        ));

        // Act & Assert
        var itemEnqueued = await this.sut.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusEnqueued, QueuedItemStatus.StatusProcessing, now);

        Assert.Null(itemEnqueued);
    }

    [Fact]
    public async Task FindOneByStatusAndUpdateStatusAtomicallyWithStatusTimestampBeforeAsync()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var firstItem = await this.sut.InsertAsync(new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { QueuedItemStatus.Processing },
            new SomePayload()
        ));

        // Act & Assert
        var itemProcessing = await this.sut.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusProcessing, QueuedItemStatus.StatusEnqueued, now, System.DateTime.UtcNow);

        Assert.NotNull(itemProcessing);
        Assert.NotNull(itemProcessing.Statuses);
        Assert.Equal(QueuedItemStatus.StatusProcessing, itemProcessing.Statuses[0].Status);

        var itemEnqueued = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, firstItem.Id)).FirstOrDefaultAsync();

        Assert.NotNull(itemEnqueued);
        Assert.NotNull(itemEnqueued.Statuses);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, itemEnqueued.Statuses[0].Status);
    }

    public class SomePayload
    {
    }
}
