using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.MongoDB;

public abstract class MongoRepository<T>
{
    static MongoRepository()
    {
        BsonSerializer.RegisterSerializationProvider(new TimestampIdSerializationProvider());
    }

    private readonly ILogger logger;
    private readonly string collectionName;
    private readonly IMongoDatabase database;
    private readonly IMongoCollection<T> collection;
    private readonly IEnumerable<CreateIndexModel<T>> indexModels;
    private bool indexesWereCreated;
    private object indexesWereCreatedLock = new();

    public MongoRepository(ILogger logger, IMongoQueueingDatabaseContext databaseContext, string collectionName) : this(
        logger, databaseContext.Database, collectionName)
    {

    }

    public MongoRepository(ILogger logger, IMongoDatabase database, string collectionName) : this(
        logger, database, database.GetCollection<T>(collectionName))
    {

    }

    public MongoRepository(ILogger logger, IMongoDatabase database, IMongoCollection<T> collection)
    {
        logger.LogDebug($"Instantiating {this.GetType().Name}...");
        this.logger = logger;
        this.collection = collection;
        this.collectionName = collection.CollectionNamespace.CollectionName;
        this.database = database;
        this.indexModels = this.GetIndexModels();
        logger.LogDebug($"EnsureIndexes {this.GetType().Name}...");
        this.EnsureIndexes();
        logger.LogDebug($"EnsureIndexes {this.GetType().Name}...done.");
        logger.LogDebug($"Instantiating {this.GetType().Name}...done.");
    }

    protected virtual IEnumerable<CreateIndexModel<T>> GetIndexModels()
    {
        return Enumerable.Empty<CreateIndexModel<T>>();
    }

    protected virtual IMongoCollection<T> Collection
    {
        get
        {
            this.EnsureIndexes();
            return this.collection;
        }
    }

    private void EnsureIndexes()
    {
        if (this.indexesWereCreated == false)
        {
            lock (this.indexesWereCreatedLock)
            {
                if (this.indexesWereCreated == false)
                {
                    try
                    {
                        if (this.indexModels.Any())
                        {
                            logger.LogDebug($"Creating Indexes {this.GetType().Name}...");
                            this.collection.Indexes.CreateMany(indexModels);
                            logger.LogDebug($"Creating Indexes {this.GetType().Name}...done.");
                        }
                        this.indexesWereCreated = true;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogCritical($"Error creating indexes for collection {this.collectionName}", ex);
                    }
                }
            }
        }
    }

    private static readonly int MAX_ITERATIONS_UNIQUE_TIMESTAMP = 1000;

    protected async Task<T> InsertWithUniqueTimestampId(Func<TimestampId, T> createEntity)
    {
        var timestampId = new TimestampId();
        for (uint i = 0; i < MAX_ITERATIONS_UNIQUE_TIMESTAMP; i++)
        {
            try
            {
                var entity = createEntity(new TimestampId(timestampId, i));
                await this.Collection.InsertOneAsync(entity);
                return entity;
            }
            catch (MongoException ex)
            {
                if (ex.IsDuplicateKeyException() == false)
                    throw;
            }
        }
        throw new Exception($"Unable to find unique TimestampId for {timestampId.Value}");
    }

    public string CollectionName => this.collectionName;

    public IMongoDatabase Database => this.database;
}
