using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Redis.Tests.Integration;

[SetUpFixture]
public sealed class RedisTestBase
{
    public static IRedisConfiguration Configuration;
    public static IDatabase Database;

    public static IConnectionMultiplexer Multiplexer;

    [OneTimeSetUp]
    public void BaseSetup()
    {
        Configuration = new RedisConfiguration(RedisSetup.ConnectionString);
        Multiplexer = ConnectionMultiplexer.Connect(
            $"{Configuration.ConnectionString},allowAdmin=true"
        );
        Database = Multiplexer.GetDatabase(Configuration.DatabaseId);
    }
}
