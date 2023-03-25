using MongoDB.Bson.Serialization;

namespace AllQuiet.MongoQueueing.MongoDB
{
    public class TimestampIdSerializer : IBsonSerializer<TimestampId>
    {
        public Type ValueType => typeof(TimestampId);


        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TimestampId value)
        {
            context.Writer.WriteInt64(Convert.ToInt64(value.Value));
        }

        public TimestampId Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return new TimestampId((ulong)context.Reader.ReadInt64());
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return this.Deserialize(context, args);
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            this.Serialize(context, args, (TimestampId)value);
        }
    }
}