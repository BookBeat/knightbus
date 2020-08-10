using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Redis.Tests.Integration
{
    [SetUpFixture, TestFixture]
    public sealed class RedisTestBase
    {
        public static RedisConfiguration Configuration;
        public static IDatabase Database;

        private IConnectionMultiplexer _multiplexer;

        [OneTimeSetUp]
        public void BaseSetup()
        {
            Configuration = new RedisConfiguration("localhost:6379");
            _multiplexer = ConnectionMultiplexer.Connect($"{Configuration.ConnectionString},allowAdmin=true");
            Database = _multiplexer.GetDatabase(Configuration.DatabaseId);
        }

        [TearDown] //This should be done after each test thus not OneTime
        public void BaseTeardown()
        {
            var server = _multiplexer.GetServer(Configuration.ConnectionString);
            server.FlushDatabase();
        }
    }
}
