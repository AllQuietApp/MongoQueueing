namespace AllQuiet.MongoQueueing.MongoDB;

public struct TimestampId : IComparable<TimestampId>
{
    private readonly ulong value;

    public TimestampId(ulong value)
    {
        this.value = value;
    }

    public TimestampId(ulong timestamp, uint counter)    
    {
        this.value = timestamp + counter;
    }

    public TimestampId(DateTime dateTime) : this(dateTime, 0)
    {
    }

    public TimestampId(DateTime dateTime, uint counter) : this(Convert.ToUInt64(dateTime.Ticks), counter)
    {
    }

    public TimestampId(TimestampId timestampId, uint counter) : this(timestampId.Value, counter)
    {
    }

    public TimestampId() : this(System.DateTime.UtcNow)
    {        
    }

    public static List<TimestampId> DistinctBatch(ICollection<TimestampId> timestampIds)
    {
        var result = new List<TimestampId>();
        var counters = new System.Collections.Generic.Dictionary<TimestampId, uint>();
        foreach(var tsId in timestampIds)
        {
            uint counter = counters.TryGetValue(tsId, out counter) ? counter : 0; 
            result.Add(new TimestampId(tsId, counter));
            counters[tsId] = counter + 1;
        }
        return result;
    }

    public ulong Value { get => this.value; }

    public int CompareTo(TimestampId other)
    {
        return this.value.CompareTo(other.Value);
    }

    public static bool operator > (TimestampId operand1, TimestampId operand2)
    {
       return operand1.CompareTo(operand2) > 0;
    }

    public static bool operator < (TimestampId operand1, TimestampId operand2)
    {
       return operand1.CompareTo(operand2) < 0;
    }

    public static bool operator >= (TimestampId operand1, TimestampId operand2)
    {
       return operand1.CompareTo(operand2) >= 0;
    }

    public static bool operator <= (TimestampId operand1, TimestampId operand2)
    {
       return operand1.CompareTo(operand2) <= 0;
    }

    public override string ToString()
    {
        return this.Value.ToString();
    }
}
