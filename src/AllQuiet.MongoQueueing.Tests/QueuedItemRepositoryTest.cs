using AllQuiet.MongoQueueing.MongoDB;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.Tests;

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
    public async Task FindOneByStatusAndUpdateStatusAtomicallyAsync_WithSpecificId()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var queuedItem1 = await this.sut.InsertAsync(new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { QueuedItemStatus.Enqueued() },
            new SomePayload()
        ));

        var queuedItem2 = await this.sut.InsertAsync(new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { QueuedItemStatus.Enqueued() },
            new SomePayload()
        ));

        // Act & Assert
        var itemEnqueued = await this.sut.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusEnqueued, QueuedItemStatus.StatusProcessing, now, null, queuedItem2.Id);

        Assert.NotNull(itemEnqueued);
        Assert.NotNull(itemEnqueued.Statuses);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, itemEnqueued.Statuses[0].Status);

        var itemProcessing = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem2.Id)).FirstOrDefaultAsync();
        var itemNotProcessing = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem1.Id)).FirstOrDefaultAsync();

        Assert.NotNull(itemProcessing);
        Assert.NotNull(itemProcessing.Statuses);
        Assert.Equal(QueuedItemStatus.StatusProcessing, itemProcessing.Statuses[0].Status);

        
        Assert.NotNull(itemNotProcessing);
        Assert.NotNull(itemNotProcessing.Statuses);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, itemNotProcessing.Statuses[0].Status);


        await this.sut.UpdateStatusAsync(queuedItem2.Id, QueuedItemStatus.Processed);
        var itemProcessed = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem2.Id)).FirstOrDefaultAsync();

        Assert.NotNull(itemProcessed);
        Assert.NotNull(itemProcessed.Statuses);
        Assert.Equal(QueuedItemStatus.StatusProcessed, itemProcessed.Statuses[0].Status);
    }

    [Fact]
    public async Task FindOneByStatusAndUpdateStatusAtomicallyAsync_WithNextReevaluation()
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
    public async Task FindOneByStatusAndUpdateStatusAtomicallyAsync_WithNextReevaluationAndQueuedItemId()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var queuedItem = await this.sut.InsertAsync(new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { QueuedItemStatus.Enqueued(now.AddMinutes(1)) },
            new SomePayload()
        ));

        // Act & Assert
        var itemEnqueued = await this.sut.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusEnqueued, QueuedItemStatus.StatusProcessing, now, null, queuedItem.Id);

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

    [Fact]
    public async Task FindOneByStatusAndUpdateStatusAtomicallyAsync_WithParallelUpdates()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var queuedItem = await this.sut.InsertAsync(new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { QueuedItemStatus.Enqueued() },
            new SomePayload()
        ));

        // Act

        var updateTasks = new List<Task<QueuedItem<SomePayload>>>();
        for (int i = 0; i<10; i++)
        {
            var task = this.sut.FindOneByStatusAndUpdateStatusAtomicallyAsync(QueuedItemStatus.StatusEnqueued, QueuedItemStatus.StatusProcessing, DateTime.UtcNow, null, queuedItem.Id);
            updateTasks.Add(task);
        }
        await Task.WhenAll(updateTasks);
        
        // Assert
        var result = updateTasks.Select(t => t.Result).Single(t => t != null);
        Assert.NotNull(result);
        Assert.Single(result.Statuses);
    }
}

public class SomePayload
{
}