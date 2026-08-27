using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.Redis;

namespace KnightBus.Redis.Tests.Integration;

[SetUpFixture]
public class RedisSetup
{
    private static readonly RedisContainer Redis = new RedisBuilder("redis")
        .WithPortBinding(6380, 6379)
        .Build();

    public static string ConnectionString = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await Redis.StartAsync();
        ConnectionString = Redis.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await Redis.DisposeAsync();
    }
}
