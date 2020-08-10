using KnightBus.Core;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Redis.Tests.Integration
{
    [TestFixture]
    public class RedisSagaStoreTests : SagaStoreTests
    {
        public override void Setup()
        {
            SagaStore = new RedisSagaStore(RedisTestBase.Database.Multiplexer, RedisTestBase.Database.Database, new JsonMessageSerializer());
        }

    }
}