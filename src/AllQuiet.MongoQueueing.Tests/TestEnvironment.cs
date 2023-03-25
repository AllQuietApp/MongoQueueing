using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AllQuiet.MongoQueueing.Tests;

public static class TestEnvironment
{
    public static IConfiguration Config { get; }

    static TestEnvironment()
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config.json", optional: true)
            .AddJsonFile("config.test.json", optional: true)
            .AddEnvironmentVariables();

        Config = configBuilder.Build();
    }

    public static IOptions<T> GetOptions<T>() where T : class, new()
    {
        var options = new T();
        Config.GetSection(typeof(T).Name).Bind(options);
        return new OptionsWrapper<T>(options);
    }
}