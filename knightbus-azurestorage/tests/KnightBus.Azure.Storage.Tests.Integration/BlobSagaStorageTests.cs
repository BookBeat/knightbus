using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Azure.Storage.Sagas;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

[TestFixture]
public class BlobSagaStorageTests : SagaStoreTests
{
    public override void Setup()
    {
        SagaStore = new BlobSagaStore("UseDevelopmentStorage=true");
    }

    [Test]
    public async Task Update_should_throw_when_etag_differs()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
        //act & assert
        await SagaStore
            .Awaiting(x => x.Update(partitionKey, id, new SagaData<Data> { Data = new Data { Message = "updated" }, ConcurrencyStamp = "etag" }, CancellationToken.None))
            .Should()
            .ThrowAsync<SagaDataConflictException>();
    }

    [Test]
    public async Task Update_should_update_when_etag_match()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var sagaData = await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
        //act
        await SagaStore.Update(partitionKey, id, new SagaData<Data> { Data = new Data { Message = "updated" }, ConcurrencyStamp = sagaData.ConcurrencyStamp }, CancellationToken.None);
        //assert
        var data = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
        data.Data.Message.Should().Be("updated");
    }

    [Test]
    public async Task Update_should_change_to_current_etag_on_success()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var sagaData = await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
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
    public async Task Complete_should_throw_when_etag_differs()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
        //act & assert
        await SagaStore
            .Awaiting(x => x.Complete(partitionKey, id, new SagaData<Data> { ConcurrencyStamp = "etag" }, CancellationToken.None))
            .Should()
            .ThrowAsync<SagaDataConflictException>();
    }

    [Test]
    public async Task Complete_should_delete_the_saga_when_etag_matches()
    {
        //arrange
        var partitionKey = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var sagaData = new SagaData<Data> { Data = new Data { Message = "yo" } };
        sagaData = await SagaStore.Create(partitionKey, id, sagaData.Data, TimeSpan.FromMinutes(1), CancellationToken.None);
        //act
        await SagaStore.Complete(partitionKey, id, new SagaData<Data> { ConcurrencyStamp = sagaData.ConcurrencyStamp }, CancellationToken.None);
        //assert
        await SagaStore
            .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
            .Should()
            .ThrowAsync<SagaNotFoundException>();
    }
}
