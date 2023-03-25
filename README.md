# MongoQueueing
Message Queueing for .NET Core with MongoDB.

![Test Workflow Status Badge](https://github.com/AllQuietApp/MongoQueueing/actions/workflows/test.yml/badge.svg)

## Motivation

### Use Cases
- You have a simple website where users can register. You want to send out a double-opt-in email asynchronously after registering.
- You want to send out a reminder email after 24h after registering.
- You want to call a third party service that can fail (your own network, downtime of service etc.). You want to retry the call in case of failure.

### Characteristics
- Supports high availability out of the box. You can run as many processes as you like. MongoDB's atomic operations ensure that messages are processed only once.
- Guarantees FIFO dequeueing but cannot guarantee order of execution of your messages when running multiple processes.
- Supports different queues, so you can have a high priority queue.
- Supports scheduling of messages (define the time when a message should be dequeued)

### What about RabbitMQ, ZeroMQ, Kafka etc?
MongoQueueing was created during the development of the [All Quiet incident escalation platform](https://allquiet.app). We wanted to keep our tech-stack as simple as possible. 
Since we were using MongoDB as a database, we didn't want to introduce more moving parts in our infrastructure. 

It's not a replacement for near-realtime queueing systems like RabbitMQ. Neither does it provide pub/sub functionalities. It's just really a dead simple mechanism to execute tasks asynchronously in your .NET Core application. Don't use it to create your fancy distributed mircoservice architecture. Use it for the above mentioned use cases.

## Usage

### Initial configuration
`Program.cs`
```c#
var builder = WebApplication.CreateBuilder(args);

// Configure QueueOptions with default polling intervals
builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection(nameof(QueueOptions)))

// Make sure to allow classes from AllQuiet namespace to be deserialized by MongoDB driver
// This is new since the MongoDB .NET Driver 2.19.0 
// https://github.com/mongodb/mongo-csharp-driver/releases/tag/v2.19.0
var objectSerializer = new ObjectSerializer(type => ObjectSerializer.DefaultAllowedTypes(type) || type.FullName?.StartsWith("AllQuiet") == true);
BsonSerializer.RegisterSerializer(objectSerializer);

// Tell MongoQueueing which MongoDB database to use

// Option 1
// Register an IMongoDatabase instance
var mongoClient = new MongoClient("mongodb://localhost:27017");
builder.Services.AddSingleton<IMongoDatabase>(mongoClient.GetDatabase("MyDatabaseName"));

// Option 2
// Register an IMongoQueuingDatabaseContext instance if you specifally need to control through DI which database should be used
var mongoClient = new MongoClient("mongodb://localhost:27017");
builder.Services.AddSingleton<IMongoQueuingDatabaseContext>(new MongoQueuingDatabaseContext(mongoClient.GetDatabase("MyDatabaseName")));
```

### Add Generic Queueing
Generic queueing will add one queue which contains different types of payloads. 
Since the queue is processed FIFO you cannot control prioritized dequeueing per payload type. If you need dedicated queues per payload type, add a dedicated queue.


`Program.cs`
```c#
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGenericQueueing();

// For each payload type you want to process in a queue, register a processor:
builder.Services.AddScoped<IGenericQueuePayloadProcessor, YourPayloadProcessor>();
builder.Services.AddScoped<IGenericQueuePayloadProcessor, YourOtherPayloadProcessor>();
...
```

`YourPayload.cs`
```c#
public class YourPayload
{
    // Any type of POCO
}
```

`YourPayloadProcessor.cs`
```c#
public class YourPayloadProcessor : GenericQueuePayloadProcessor<YourPayload>
{
    protected override async Task ProcessAsync(YourPayload payload)
    {
        // Do your processing here
    }
}
```

Enqueue new payloads by using IGenericQueue
```c#
public class YourService
{
    private readonly IGenericQueue genericQueue;
    public YourService(IGenericQueue genericQueue)
    {
        this.genericQueue = genericQueue;
    }

    public async Task EnqueueSomething()
    {
        await this.genericQueue.EnqueueAsync(new YourPayload());
    }
}
```

### Add Dedicated Queueing
**Attention:** Be careful to add too many dedicated queues because each queue will add a .NET Background Service that will periodically poll your mongo database.

`Program.cs`
```c#
// This will add a dedicated queue which will only contain payloads of type YourPayload.
builder.Services.AddDedicatedQueueingFor<YourPayload, YourPayloadProcessor>();
```

Enqueue new payloads by using `IQueue<T>`
```c#
public class YourService
{
    private readonly IQueue<YourPayload> yourPayloadQueue;
    public YourService(IQueue<YourPayload> yourPayloadQueue)
    {
        this.yourPayloadQueue = yourPayloadQueue;
    }

    public async Task EnqueueSomething()
    {
        await this.yourPayloadQueue.EnqueueAsync(new YourPayload());
    }
}
```

### Schedule future processing
You can easily enqueue a payload that should be processed in the future:
```c#
public class YourService
{
    ... 

    public async Task EnqueueTomorrow()
    {
        await this.yourPayloadQueue.EnqueueAsync(new YourPayload(), DateTime.UtcNow.AddHours(24));
    }
}
```

## Running Tests

### Run MongoDB locally
To run the integration tests you need a running mongo instance which you can connect to. If you have docker installed, an easy way to do this is to simply spin up a container:
    
    docker run --name mongo -d --restart unless-stopped -p 27017:27017 mongo:6.0.2

The command above will start a mongo container listening on the default port 27017. Docker will keep the container running and preserves its state during restarts of the docker host (and your computer).

If you already have a running MongoDB that you'd like to use for the integration tests, you can modify the connection string in: `./src/AllQuiet.MongoQueueing.Tests/config.json`

### Run the tests

Run the unit and integration tests in the project's root (where the sln is located):

    dotnet test

## Contributing
If you encounter a bug or have a feature request, please use our [Issue Tracker](https://github.com/AllQuietApp/MongoQueueing/issues/new) at GitHub. 
The project is also open to contributions, so feel free to fork the project and open pull requests.
    
## License
MIT License

Copyright (c) 2023 All Quiet GmbH

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
