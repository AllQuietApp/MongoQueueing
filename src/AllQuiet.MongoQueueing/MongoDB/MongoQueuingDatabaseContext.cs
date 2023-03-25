using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.MongoDB;

public class MongoQueuingDatabaseContext : IMongoQueuingDatabaseContext
{
    public MongoQueuingDatabaseContext(IMongoDatabase database)
    {
        this.Database = database;
    }

    public IMongoDatabase Database { get; private set; }
}

public interface IMongoQueuingDatabaseContext
{
    IMongoDatabase Database { get; }
}