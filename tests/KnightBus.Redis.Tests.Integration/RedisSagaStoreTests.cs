using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Redis.Tests.Integration;

[TestFixture]
public class RedisSagaStoreTests : ConcurrentSagaStoreTests
{
    private IRedisConfiguration _configuration = null!;

    public override void Setup()
    {
        _configuration = new RedisConfiguration { DatabaseId = RedisTestBase.Database.Database };
        SagaStore = new RedisSagaStore(RedisTestBase.Multiplexer, _configuration);
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

    // Not in the shared fixture: a conditional delete of a missing blob gets a 412 from Azure, so
    // the Blob store reports a conflict here.
    [Test]
    public async Task Complete_should_throw_not_found_before_conflict_when_saga_is_missing()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Complete(
                    partitionKey,
                    id,
                    new SagaData<Data> { ConcurrencyStamp = "stale" },
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<SagaNotFoundException>();
    }

    [Test]
    public async Task Create_should_set_the_ttl()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        //act
        await SagaStore.Create(
            partitionKey,
            id,
            new Data { Message = "yo" },
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //assert
        var ttl = await RedisTestBase.Database.KeyTimeToLiveAsync(Key(partitionKey, id));
        ttl.Should().NotBeNull();
        ttl.Value.Should()
            .BeGreaterThan(TimeSpan.FromSeconds(50))
            .And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task Update_should_preserve_the_ttl()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SagaStore.Create(
            partitionKey,
            id,
            new Data { Message = "yo" },
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //act
        await SagaStore.Update(
            partitionKey,
            id,
            new SagaData<Data> { Data = new Data { Message = "updated" } },
            CancellationToken.None
        );
        //assert
        var ttl = await RedisTestBase.Database.KeyTimeToLiveAsync(Key(partitionKey, id));
        ttl.Should().NotBeNull();
        ttl.Value.Should()
            .BeGreaterThan(TimeSpan.FromSeconds(50))
            .And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task Create_should_store_a_hash_with_data_and_stamp_fields()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        //act
        var created = await SagaStore.Create(
            partitionKey,
            id,
            new Data { Message = "yo" },
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //assert
        var key = Key(partitionKey, id);
        (await RedisTestBase.Database.KeyTypeAsync(key)).Should().Be(RedisType.Hash);
        var fields = await RedisTestBase.Database.HashGetAsync(
            key,
            [RedisSagaStore.DataField, RedisSagaStore.StampField]
        );
        _configuration
            .MessageSerializer.Deserialize<Data>((ReadOnlyMemory<byte>)fields[0])
            .Message.Should()
            .Be("yo");
        ((string?)fields[1]).Should().Be(created.ConcurrencyStamp);
    }

    [Test]
    public async Task Update_should_write_unconditionally_when_the_stored_stamp_is_missing()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await RedisTestBase.Database.HashSetAsync(
            Key(partitionKey, id),
            RedisSagaStore.DataField,
            _configuration.MessageSerializer.Serialize(new Data { Message = "yo" })
        );
        //act
        await SagaStore.Update(
            partitionKey,
            id,
            new SagaData<Data> { Data = new Data { Message = "updated" } },
            CancellationToken.None
        );
        //assert
        var saga = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        saga.Data.Message.Should().Be("updated");
        saga.ConcurrencyStamp.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Update_should_survive_a_script_flush()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var sagaData = await SagaStore.Create(
            partitionKey,
            id,
            new Data { Message = "yo" },
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        sagaData.Data.Message = "updated";
        await SagaStore.Update(partitionKey, id, sagaData, CancellationToken.None);
        foreach (var server in RedisTestBase.Multiplexer.GetServers())
            await server.ScriptFlushAsync();
        //act
        sagaData.Data.Message = "updated again";
        await SagaStore.Update(partitionKey, id, sagaData, CancellationToken.None);
        //assert
        var data = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        data.Data.Message.Should().Be("updated again");
    }

    [Test]
    public async Task GetSaga_should_fail_with_wrongtype_for_a_saga_written_by_15x()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SeedLegacySaga(partitionKey, id);
        //act & assert
        await SagaStore
            .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
            .Should()
            .ThrowAsync<RedisServerException>()
            .Where(e => e.Message.Contains("WRONGTYPE"));
    }

    [Test]
    public async Task Create_should_fail_with_wrongtype_for_a_saga_written_by_15x()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SeedLegacySaga(partitionKey, id);
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Create(
                    partitionKey,
                    id,
                    new Data { Message = "yo" },
                    TimeSpan.FromMinutes(1),
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<RedisServerException>()
            .Where(e => e.Message.Contains("WRONGTYPE"));
    }

    [Test]
    public async Task Update_should_fail_with_wrongtype_for_a_saga_written_by_15x()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SeedLegacySaga(partitionKey, id);
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Update(
                    partitionKey,
                    id,
                    new SagaData<Data> { Data = new Data { Message = "updated" } },
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<RedisServerException>()
            .Where(e => e.Message.Contains("WRONGTYPE"));
    }

    [Test]
    public async Task Complete_should_fail_with_wrongtype_for_a_saga_written_by_15x()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SeedLegacySaga(partitionKey, id);
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Complete(partitionKey, id, new SagaData<Data>(), CancellationToken.None)
            )
            .Should()
            .ThrowAsync<RedisServerException>()
            .Where(e => e.Message.Contains("WRONGTYPE"));
    }

    [Test]
    public async Task Delete_should_remove_a_saga_written_by_15x()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SeedLegacySaga(partitionKey, id);
        //act
        await SagaStore.Delete(partitionKey, id, CancellationToken.None);
        //assert
        (await RedisTestBase.Database.KeyExistsAsync(Key(partitionKey, id)))
            .Should()
            .BeFalse();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Create_should_throw_when_ttl_is_not_positive(int seconds)
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Create(
                    partitionKey,
                    id,
                    new Data { Message = "yo" },
                    TimeSpan.FromSeconds(seconds),
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Should_throw_when_partition_key_or_id_is_empty()
    {
        await SagaStore
            .Awaiting(x => x.GetSaga<Data>("", "id", CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentException>();
        await SagaStore
            .Awaiting(x => x.GetSaga<Data>("partition", null!, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task Should_throw_when_the_token_is_already_cancelled()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var sagaData = new SagaData<Data> { Data = new Data { Message = "yo" } };
        var cancelled = new CancellationToken(true);
        //act & assert
        await SagaStore
            .Awaiting(x => x.GetSaga<Data>(partitionKey, id, cancelled))
            .Should()
            .ThrowAsync<OperationCanceledException>();
        await SagaStore
            .Awaiting(x =>
                x.Create(partitionKey, id, sagaData.Data, TimeSpan.FromMinutes(1), cancelled)
            )
            .Should()
            .ThrowAsync<OperationCanceledException>();
        await SagaStore
            .Awaiting(x => x.Update(partitionKey, id, sagaData, cancelled))
            .Should()
            .ThrowAsync<OperationCanceledException>();
        await SagaStore
            .Awaiting(x => x.Complete(partitionKey, id, sagaData, cancelled))
            .Should()
            .ThrowAsync<OperationCanceledException>();
        await SagaStore
            .Awaiting(x => x.Delete(partitionKey, id, cancelled))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    private Task SeedLegacySaga(string partitionKey, string id) =>
        RedisTestBase.Database.StringSetAsync(
            Key(partitionKey, id),
            _configuration.MessageSerializer.Serialize(new Data { Message = "legacy" }),
            TimeSpan.FromMinutes(1)
        );

    private static RedisKey Key(string partitionKey, string id) =>
        RedisQueueConventions.GetSagaKey(partitionKey, id);
}
