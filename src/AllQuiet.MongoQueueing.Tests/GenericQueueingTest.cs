using System.Collections.Concurrent;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.Tests.Queuing;

public class GenericQueueingTest : MongoDBTest
{
    private IGenericQueue genericQueue;

    public GenericQueueingTest()
    {
        BsonSerializer.RegisterSerializer(new ObjectSerializer(type => true));

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IOptions<QueueOptions>>(new OptionsWrapper<QueueOptions>(new QueueOptions()));
        serviceCollection.AddSingleton<ILogger<Queue<GenericQueueEvent>>>(A.Fake<ILogger<Queue<GenericQueueEvent>>>());
        serviceCollection.AddSingleton<ILogger<QueuedItemRepository<GenericQueueEvent>>>(A.Fake<ILogger<QueuedItemRepository<GenericQueueEvent>>>());
        serviceCollection.AddSingleton<IMongoDatabase>(this.MongoDatabase);
        serviceCollection.AddGenericQueueing();


        var provider = serviceCollection.BuildServiceProvider();
        this.genericQueue = provider.GetRequiredService<IGenericQueue>();
    }

    [Fact]
    public async Task Enqueue_Enqueues()
    {
        // Arrange
        var payload = new Payload { Id = Guid.NewGuid() };

        // Act
        var queuedItem = await this.genericQueue.EnqueueAsync(payload);
    
        // Assert
        Assert.NotNull(queuedItem);
    }

    public class Payload
    {
        public Guid Id { get; set; }
    }

    public class PayloadProcessor : GenericQueuePayloadProcessor<Payload>
    {
        public ConcurrentBag<Payload> ProcessedPayloads { get; init; } = new ConcurrentBag<Payload>();
        protected override Task ProcessAsync(Payload payload)
        {
            ProcessedPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }
}
