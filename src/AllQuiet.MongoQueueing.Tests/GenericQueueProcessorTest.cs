using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace AllQuiet.MongoQueueing.Tests.Queuing;

public class GenericQueueProcessorTest
{
    private readonly GenericQueueProcessor sut;
    private readonly IGenericQueuePayloadProcessor payloadProcessor1;
    private readonly IGenericQueuePayloadProcessor payloadProcessor2;

    public GenericQueueProcessorTest()
    {
        this.payloadProcessor1 = A.Fake<IGenericQueuePayloadProcessor>();
        this.payloadProcessor2 = A.Fake<IGenericQueuePayloadProcessor>();

        this.sut = new GenericQueueProcessor(
            A.Dummy<ILogger<GenericQueueProcessor>>(),
            new []
            {
                this.payloadProcessor1,
                this.payloadProcessor2
            });
    }

    [Fact]
    public async Task Enqueue_Enqueues()
    {
        // Arrange
        var payload = new GenericQueueEvent { Payload = new DummyPayload() };
        
        A.CallTo(() => this.payloadProcessor1.CanProcess(A<object>._)).Returns(false);
        A.CallTo(() => this.payloadProcessor2.CanProcess(A<object>.That.IsInstanceOf(typeof(DummyPayload)))).Returns(true);
        
        // Act
        await this.sut.ProcessQueuedItemAsync(payload);
    
        // Assert
        A.CallTo(() => this.payloadProcessor1.ProcessAsync(A<object>._)).MustNotHaveHappened();
        A.CallTo(() => this.payloadProcessor2.ProcessAsync(payload.Payload)).MustHaveHappenedOnceExactly();
    }

}

internal class DummyPayload
{
    public DummyPayload()
    {
    }
}