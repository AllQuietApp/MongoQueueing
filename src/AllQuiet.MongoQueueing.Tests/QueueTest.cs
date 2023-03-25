using AllQuiet.MongoQueueing.MongoDB;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.Tests.Queuing;

public class QueueTest : MongoDBTest
{
    private readonly IMongoCollection<QueuedItem<SomePayload>> collection;
    private readonly Queue<SomePayload> sut;

    public QueueTest()
    {
        var collectionName = "queuedItemsSomePayload";
        var repository = new QueuedItemRepository<SomePayload>(A.Dummy<ILogger<QueuedItemRepository<SomePayload>>>(), this.MongoDatabase, collectionName);
        this.collection = this.MongoDatabase.GetCollection<QueuedItem<SomePayload>>(collectionName);
        this.sut = new Queue<SomePayload>(A.Fake<ILogger<Queue<SomePayload>>>(), repository, new OptionsWrapper<QueueOptions>(new QueueOptions())); 
    }

    [Fact]
    public async Task Enqueue_Enqueues()
    {
        // Arrange
        var payload = new SomePayload { SomeString = "abc123" };

        // Act
        var queuedItem = await this.sut.EnqueueAsync(payload);
    
        // Assert
        Assert.NotNull(queuedItem);
        Assert.NotNull(queuedItem.Statuses);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, queuedItem.Statuses[0].Status);
        Assert.Equal(payload.SomeString, queuedItem.Payload.SomeString);
    }

    [Fact]
    public async Task Dequeue_ProcessingSuccesful_SetsToProcessed()
    {
        // Arrange
        var payload = new SomePayload { SomeString = "abc123" };
        var queuedItem = await this.sut.EnqueueAsync(payload);

        // Act
        await this.sut.DequeueAsync(payload => Task.CompletedTask);

        // Assert
        var queuedItemAfterDequeue = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();
        
        Assert.NotNull(queuedItemAfterDequeue);
        Assert.Equal(QueuedItemStatus.StatusProcessed, queuedItemAfterDequeue.Statuses[0].Status);
        Assert.Equal(QueuedItemStatus.StatusProcessing, queuedItemAfterDequeue.Statuses[1].Status);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, queuedItemAfterDequeue.Statuses[2].Status);
    }

    [Fact]
    public async Task Dequeue_ProcessingWithException_SetsToFailed()
    {
        // Arrange
        var payload = new SomePayload { SomeString = "abc123" };
        var queuedItem = await this.sut.EnqueueAsync(payload);

        // Act & Assert
        await this.sut.DequeueAsync(payload => {
            throw new Exception("Something went terribly wrong.");
        });

        var queuedItemAfterDequeue = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();
        
        Assert.NotNull(queuedItemAfterDequeue);
        Assert.Equal(QueuedItemStatus.StatusFailed, queuedItemAfterDequeue.Statuses[0].Status);
        Assert.NotNull(queuedItemAfterDequeue.Statuses[0].NextReevaluation);
        Assert.True(queuedItemAfterDequeue.Statuses[0].NextReevaluation >= queuedItemAfterDequeue.Statuses[0].Timestamp, 
            $"Expected {queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value} ({queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value.Ticks }) to be gte {queuedItemAfterDequeue.Statuses[0].Timestamp} ({queuedItemAfterDequeue.Statuses[0].Timestamp.Ticks})");

        Assert.Equal(QueuedItemStatus.StatusProcessing, queuedItemAfterDequeue.Statuses[1].Status);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, queuedItemAfterDequeue.Statuses[2].Status);
    }

    [Fact]
    public async Task EnqueueOrphanedProcessingAsync()
    {
        // Arrange
        var item = new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { new QueuedItemStatus(QueuedItemStatus.StatusProcessing, DateTime.UtcNow.AddMinutes(-60)) },
            new SomePayload()
        );
        await this.collection.InsertOneAsync(item);

        // Act
        var itemProcessing = await this.sut.EnqueueOrphanedProcessingAsync();

        // Assert
        
        var itemAfterEnqueueOrphaned = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, item.Id)).FirstOrDefaultAsync();

        Assert.NotNull(itemAfterEnqueueOrphaned);
        Assert.NotNull(itemAfterEnqueueOrphaned.Statuses);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, itemAfterEnqueueOrphaned.Statuses[0].Status);
    }

    public class SomePayload
    {
        public string? SomeString { get; set; }
    }
}
