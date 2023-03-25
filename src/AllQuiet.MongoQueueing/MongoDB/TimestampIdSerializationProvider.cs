using MongoDB.Bson.Serialization;

namespace AllQuiet.MongoQueueing.MongoDB;

public class TimestampIdSerializationProvider : IBsonSerializationProvider
{
    public IBsonSerializer? GetSerializer(Type type)
    {
        if (type == typeof(TimestampId))
        {
            return new TimestampIdSerializer();
        }        

        return null;
    }
}
