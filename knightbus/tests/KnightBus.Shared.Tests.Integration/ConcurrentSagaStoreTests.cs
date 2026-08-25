using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using NUnit.Framework;

namespace KnightBus.Shared.Tests.Integration;

[TestFixture]
public class ConcurrentSagaStoreTests : SagaStoreTests
{
    [Test]
    public async Task Update_should_throw_when_stamp_differs()
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
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Update(
                    partitionKey,
                    id,
                    new SagaData<Data>
                    {
                        Data = new Data { Message = "updated" },
                        ConcurrencyStamp = "stale",
                    },
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<SagaDataConflictException>();
    }

    [Test]
    public async Task Update_should_update_when_stamp_matches()
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
        //act
        await SagaStore.Update(
            partitionKey,
            id,
            new SagaData<Data>
            {
                Data = new Data { Message = "updated" },
                ConcurrencyStamp = sagaData.ConcurrencyStamp,
            },
            CancellationToken.None
        );
        //assert
        var data = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        data.Data.Message.Should().Be("updated");
    }

    [Test]
    public async Task Update_should_change_to_current_stamp_on_success()
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
        //act
        sagaData.Data.Message = "updated";
        await SagaStore.Update(partitionKey, id, sagaData, CancellationToken.None);
        sagaData.Data.Message = "updated again";
        await SagaStore.Update(partitionKey, id, sagaData, CancellationToken.None);
        //assert
        var data = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        data.Data.Message.Should().Be("updated again");
    }

    [Test]
    public async Task Complete_should_throw_when_stamp_differs()
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
            .ThrowAsync<SagaDataConflictException>();
    }

    [Test]
    public async Task Complete_should_delete_the_saga_when_stamp_matches()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var sagaData = new SagaData<Data> { Data = new Data { Message = "yo" } };
        sagaData = await SagaStore.Create(
            partitionKey,
            id,
            sagaData.Data,
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //act
        await SagaStore.Complete(
            partitionKey,
            id,
            new SagaData<Data> { ConcurrencyStamp = sagaData.ConcurrencyStamp },
            CancellationToken.None
        );
        //assert
        await SagaStore
            .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
            .Should()
            .ThrowAsync<SagaNotFoundException>();
    }

    [Test]
    public async Task Complete_should_keep_the_saga_when_stamp_differs()
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
            .ThrowAsync<SagaDataConflictException>();
        //assert
        var saga = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        saga.Data.Message.Should().Be("yo");
    }

    [Test]
    public async Task Complete_should_delete_the_saga_when_stamp_is_null()
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
        await SagaStore.Complete(partitionKey, id, new SagaData<Data>(), CancellationToken.None);
        //assert
        await SagaStore
            .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
            .Should()
            .ThrowAsync<SagaNotFoundException>();
    }

    [Test]
    public async Task Create_should_return_a_concurrency_stamp()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        //act
        var sagaData = await SagaStore.Create(
            partitionKey,
            id,
            new Data { Message = "yo" },
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //assert
        sagaData.ConcurrencyStamp.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task GetSaga_should_return_the_stored_concurrency_stamp()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var created = await SagaStore.Create(
            partitionKey,
            id,
            new Data { Message = "yo" },
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //act
        var loaded = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        //assert
        loaded.ConcurrencyStamp.Should().Be(created.ConcurrencyStamp);
    }

    [Test]
    public async Task Update_should_reject_the_previous_stamp_after_success()
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
        var previousStamp = sagaData.ConcurrencyStamp;
        sagaData.Data.Message = "updated";
        await SagaStore.Update(partitionKey, id, sagaData, CancellationToken.None);
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Update(
                    partitionKey,
                    id,
                    new SagaData<Data>
                    {
                        Data = new Data { Message = "stale write" },
                        ConcurrencyStamp = previousStamp,
                    },
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<SagaDataConflictException>();
    }

    [Test]
    public async Task Update_should_throw_not_found_before_conflict_when_saga_is_missing()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        //act & assert
        await SagaStore
            .Awaiting(x =>
                x.Update(
                    partitionKey,
                    id,
                    new SagaData<Data>
                    {
                        Data = new Data { Message = "yo" },
                        ConcurrencyStamp = "stale",
                    },
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<SagaNotFoundException>();
    }

    [Test]
    public async Task Concurrent_updates_with_the_same_stamp_should_fail_exactly_once()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var created = await SagaStore.Create(
            partitionKey,
            id,
            new Data { Message = "yo" },
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        var first = new SagaData<Data>
        {
            Data = new Data { Message = "first" },
            ConcurrencyStamp = created.ConcurrencyStamp,
        };
        var second = new SagaData<Data>
        {
            Data = new Data { Message = "second" },
            ConcurrencyStamp = created.ConcurrencyStamp,
        };
        //act
        var conflicts = await Task.WhenAll(
            TryUpdate(partitionKey, id, first),
            TryUpdate(partitionKey, id, second)
        );
        //assert
        conflicts.Should().ContainSingle(e => e == null);
        conflicts.Should().ContainSingle(e => e != null);
        var winner = conflicts[0] == null ? first : second;
        var stored = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        stored.Data.Message.Should().Be(winner.Data.Message);
        stored.ConcurrencyStamp.Should().Be(winner.ConcurrencyStamp);
    }

    private async Task<SagaDataConflictException> TryUpdate(
        string partitionKey,
        string id,
        SagaData<Data> sagaData
    )
    {
        try
        {
            await SagaStore.Update(partitionKey, id, sagaData, CancellationToken.None);
            return null;
        }
        catch (SagaDataConflictException e)
        {
            return e;
        }
    }
}
