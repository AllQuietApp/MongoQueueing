using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace AllQuiet.MongoQueueing.Tests
{
    public abstract class MongoDBTest : IDisposable
    {
        private static readonly Random randmon = new Random(Environment.TickCount);
        private readonly string databaseName;
        private readonly MongoClient client;
        private readonly IMongoDatabase mongoDatabase;


        protected MongoDBTest()
        {
            this.databaseName = $"t_{DateTime.Now.Ticks + randmon.Next(100000)}";
            this.client = new MongoClient(TestEnvironment.Config.GetConnectionString("MongoDbIntegrationTests"));
            this.mongoDatabase = client.GetDatabase(this.databaseName);
        }

        protected IMongoDatabase MongoDatabase { get { return this.mongoDatabase; } }

        public void Dispose()
        {
            this.client.DropDatabase(this.databaseName);
        }
    }
}