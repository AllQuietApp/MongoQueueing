using MongoDB.Driver;

namespace AllQuiet.MongoQueueing
{
    public static class MongoExceptionExtensions
    {    
        private const int WRITE_ERROR_CODE_DUPLICATE_KEY = 11000;

        /// <summary>
        /// Returns true if the MongoException represents a duplicate key exception.
        /// </summary>
        public static bool IsDuplicateKeyException(this MongoException mongoException)
        {
            if (mongoException is MongoDuplicateKeyException)
            {
                return true;
            }

            var mongoWriteException = mongoException as MongoWriteException;
            if (mongoWriteException?.WriteError?.Code == WRITE_ERROR_CODE_DUPLICATE_KEY)
            {
                return true;
            }
            

            return false;
        }
    }
}