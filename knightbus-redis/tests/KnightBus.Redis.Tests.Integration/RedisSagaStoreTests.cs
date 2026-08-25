using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.Redis.Tests.Integration;

[TestFixture]
public class RedisSagaStoreTests : SagaStoreTests
{
    public override void Setup()
    {
        SagaStore = new RedisSagaStore(
            RedisTestBase.Database.Multiplexer,
            new RedisConfiguration { DatabaseId = RedisTestBase.Database.Database }
        );
    }

    [Test]
    public async Task Delete_should_throw_when_saga_not_found()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        //act & assert
        await SagaStore
            .Awaiting(x => x.Delete(partitionKey, id, CancellationToken.None))
            .Should()
            .ThrowAsync<SagaNotFoundException>();
    }
}
