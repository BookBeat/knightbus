using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Redis.Tests.Integration
{
    [TestFixture]
    public class RedisTestBase
    {
        protected RedisConfiguration Configuration;
        protected IDatabase Database;

        private IConnectionMultiplexer _multiplexer;

        [SetUp]
        public void BaseSetup()
        {
            Configuration = new RedisConfiguration("localhost:6379");
            _multiplexer = ConnectionMultiplexer.Connect($"{Configuration.ConnectionString},allowAdmin=true");
            Database = _multiplexer.GetDatabase(Configuration.DatabaseId);
        }

        [TearDown]
        public void BaseTeardown()
        {
            var server = _multiplexer.GetServer(Configuration.ConnectionString);
            server.FlushDatabase();
        }
    }
}
