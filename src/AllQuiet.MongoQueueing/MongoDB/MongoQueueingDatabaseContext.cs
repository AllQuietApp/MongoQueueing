using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.MongoDB;

/// <summary>
/// Provides a wrapper around an IMongoDatabase. Implementation of the marker interface <c>IMongoQueueingDatabaseContext</c>.
/// By registering an instance with the DI container you can control what database you want to pass to MongoQueueing.
/// </summary>
public class MongoQueueingDatabaseContext : IMongoQueueingDatabaseContext
{
    public MongoQueueingDatabaseContext(IMongoDatabase database)
    {
        this.Database = database;
    }

    public IMongoDatabase Database { get; private set; }
}

/// <summary>
/// Marker interface to wrap an IMongoDatabase. By implementing this interface and registering it with the DI container you can control what database you want to pass to MongoQueueing.
/// </summary>
public interface IMongoQueueingDatabaseContext
{
    IMongoDatabase Database { get; }
}