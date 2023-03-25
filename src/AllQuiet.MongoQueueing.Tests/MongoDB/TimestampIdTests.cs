using AllQuiet.MongoQueueing.MongoDB;

namespace AllQuiet.MongoQueueing.Tests.MongoDB;

public class TimestampIdTests
{

    [Fact]
    public void DistinctBatch()
    {
        var utcNow = new DateTime(637_892_510_918_598_860);
        
        var timeStamps = new [] {
            new TimestampId(utcNow),
            new TimestampId(utcNow),
            new TimestampId(utcNow.AddMilliseconds(10)),
            new TimestampId(utcNow)
        };

        var distinctBatch = TimestampId.DistinctBatch(timeStamps);

        Assert.True(distinctBatch[0] < distinctBatch[1]);
        Assert.True(distinctBatch[1] < distinctBatch[2]);
        Assert.True(distinctBatch[2] > distinctBatch[3]);
        Assert.True(distinctBatch[3] > distinctBatch[1]);
    }

    [Fact]
    public void Test()
    {
        var utcNow = new DateTime(637_892_510_918_598_860);
        var timestampId0 = new TimestampId(utcNow, 0);
        var timestampId1 = new TimestampId(utcNow, 1);

        Assert.True(timestampId0 < timestampId1);
    }

    [Fact]
    public void Constructor()
    {
        var utcNow = new DateTime(637_892_510_918_598_860);
        var timestampId = new TimestampId(utcNow);

        Assert.Equal((ulong)637_892_510_918_598_860, timestampId.Value);
    }

    [Fact]
    public void CompareEquals()
    {
        var a = new TimestampId(2);
        var b = new TimestampId(2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void CompareLt()
    {
        var a = new TimestampId(1);
        var b = new TimestampId(2);

        Assert.True(a < b, "a should be less than b");
        Assert.False(a >= b, "a should be less than b");
    }

    [Fact]
    public void CompareLte_Lt()
    {
        var a = new TimestampId(1);
        var b = new TimestampId(2);

        Assert.True(a <= b, "a should be less than equal b");
        Assert.False(a > b, "a should be less than equal to b");
    }

    [Fact]
    public void CompareLte_Equal()
    {
        var a = new TimestampId(2);
        var b = new TimestampId(2);

        Assert.True(a <= b, "a should be less than equal to b");
        Assert.False(a > b, "a should be less than equal to b");
    }

    [Fact]
    public void CompareGt()
    {
        var a = new TimestampId(2);
        var b = new TimestampId(1);

        Assert.True(a > b, "a should be greater than b");
        Assert.False(a <= b, "a should be greater than b");
    }

    [Fact]
    public void CompareGte_Gt()
    {
        var a = new TimestampId(2);
        var b = new TimestampId(1);

        Assert.True(a >= b, "a should be greater than equal b");
        Assert.False(a < b, "a should be greater than equal b");
    }

    [Fact]
    public void CompareGte_Equal()
    {
        var a = new TimestampId(2);
        var b = new TimestampId(2);

        Assert.True(a >= b, "a should be greater than equal to b");
        Assert.False(a < b, "a should be greater than equal to b");
    }
}