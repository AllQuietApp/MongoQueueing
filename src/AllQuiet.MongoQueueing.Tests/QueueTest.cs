using AllQuiet.MongoQueueing.MongoDB;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.Tests.Queuing;

public class QueueTest : MongoDBTest
{
    private readonly QueuedItemRepository<SomePayload> repository;
    private readonly IMongoCollection<QueuedItem<SomePayload>> collection;

    public QueueTest()
    {
        var collectionName = "queuedItemsSomePayload";
        this.repository = new QueuedItemRepository<SomePayload>(A.Dummy<ILogger<QueuedItemRepository<SomePayload>>>(), this.MongoDatabase, collectionName);
        this.collection = this.MongoDatabase.GetCollection<QueuedItem<SomePayload>>(collectionName);
    }

    private Queue<SomePayload> Sut(QueueOptions? options = null)
    {
        return new Queue<SomePayload>(A.Fake<ILogger<Queue<SomePayload>>>(), repository, new OptionsWrapper<QueueOptions>(options ?? new QueueOptions())); 
    }

    [Fact]
    public async Task Enqueue_Enqueues()
    {
        // Arrange
        var payload = new SomePayload { SomeString = "abc123" };
        var sut = this.Sut();

        // Act
        var queuedItem = await sut.EnqueueAsync(payload);
    
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
        var sut = this.Sut();
        var payload = new SomePayload { SomeString = "abc123" };
        var queuedItem = await sut.EnqueueAsync(payload);

        // Act
        await sut.DequeueAsync(null, payload => Task.CompletedTask);

        // Assert
        var queuedItemAfterDequeue = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();
        
        Assert.NotNull(queuedItemAfterDequeue);
        Assert.Equal(QueuedItemStatus.StatusProcessed, queuedItemAfterDequeue.Statuses[0].Status);
        Assert.Equal(QueuedItemStatus.StatusProcessing, queuedItemAfterDequeue.Statuses[1].Status);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, queuedItemAfterDequeue.Statuses[2].Status);
    }

    [Fact]
    public async Task Dequeue_ProcessingSuccesful_WithClearMessages_Removes()
    {
        // Arrange
        var sut = this.Sut(new QueueOptions { ClearSuccessfulMessages = true });
        var payload = new SomePayload { SomeString = "abc123" };
        var queuedItem = await sut.EnqueueAsync(payload);

        // Act
        await sut.DequeueAsync(null, payload => Task.CompletedTask);

        // Assert
        var queuedItemAfterDequeue = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();
        
        Assert.Null(queuedItemAfterDequeue);
    }

    [Fact]
    public async Task Dequeue_ProcessingWithException_WithPersistException_SetsToFailedWithException()
    {
        // Arrange
        var sut = this.Sut(new QueueOptions { PersistException = true });

        var payload = new SomePayload { SomeString = "abc123" };
        var queuedItem = await sut.EnqueueAsync(payload);

        // Act & Assert
        await sut.DequeueAsync(null, payload => {
            throw new Exception("Something went terribly wrong.");
        });

        var queuedItemAfterDequeue = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();
        
        Assert.NotNull(queuedItemAfterDequeue);
        Assert.Equal(QueuedItemStatus.StatusFailed, queuedItemAfterDequeue.Statuses[0].Status);
        Assert.NotNull(queuedItemAfterDequeue.Statuses[0].Exception);
        Assert.Equal("Something went terribly wrong.", queuedItemAfterDequeue.Statuses[0].Exception!.Message);
        Assert.NotNull(queuedItemAfterDequeue.Statuses[0].NextReevaluation);
        Assert.True(queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value >= queuedItemAfterDequeue.Statuses[0].Timestamp, 
            $"Expected {queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value} ({queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value.Ticks }) to be gte {queuedItemAfterDequeue.Statuses[0].Timestamp} ({queuedItemAfterDequeue.Statuses[0].Timestamp.Ticks})");

        Assert.Equal(QueuedItemStatus.StatusProcessing, queuedItemAfterDequeue.Statuses[1].Status);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, queuedItemAfterDequeue.Statuses[2].Status);
    }

    [Fact]
    public async Task Dequeue_ProcessingWithException_SetsToFailed()
    {
        // Arrange
        var sut = this.Sut();
        var payload = new SomePayload { SomeString = "abc123" };
        var queuedItem = await sut.EnqueueAsync(payload);

        // Act & Assert
        await sut.DequeueAsync(null, payload => {
            throw new Exception("Something went terribly wrong.");
        });

        var queuedItemAfterDequeue = await this.collection.Find(Builders<QueuedItem<SomePayload>>.Filter.Eq(item => item.Id, queuedItem.Id)).FirstOrDefaultAsync();
        
        Assert.NotNull(queuedItemAfterDequeue);
        Assert.Equal(QueuedItemStatus.StatusFailed, queuedItemAfterDequeue.Statuses[0].Status);
        Assert.NotNull(queuedItemAfterDequeue.Statuses[0].NextReevaluation);
        Assert.True(queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value >= queuedItemAfterDequeue.Statuses[0].Timestamp, 
            $"Expected {queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value} ({queuedItemAfterDequeue.Statuses[0].NextReevaluation!.Value.Ticks }) to be gte {queuedItemAfterDequeue.Statuses[0].Timestamp} ({queuedItemAfterDequeue.Statuses[0].Timestamp.Ticks})");

        Assert.Equal(QueuedItemStatus.StatusProcessing, queuedItemAfterDequeue.Statuses[1].Status);
        Assert.Equal(QueuedItemStatus.StatusEnqueued, queuedItemAfterDequeue.Statuses[2].Status);
    }

    [Fact]
    public async Task EnqueueOrphanedProcessingAsync()
    {
        // Arrange
        var sut = this.Sut();
        var item = new QueuedItem<SomePayload>(
            new TimestampId(),
            new [] { new QueuedItemStatus(QueuedItemStatus.StatusProcessing, DateTime.UtcNow.AddMinutes(-60)) },
            new SomePayload()
        );
        await this.collection.InsertOneAsync(item);

        // Act
        var itemProcessing = await sut.EnqueueOrphanedProcessingAsync();

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
