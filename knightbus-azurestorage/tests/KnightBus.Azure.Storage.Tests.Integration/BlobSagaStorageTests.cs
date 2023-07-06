using FluentAssertions;
using System.Threading.Tasks;
using System;
using KnightBus.Azure.Storage.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;
using KnightBus.Core.Sagas;

namespace KnightBus.Azure.Storage.Tests.Integration
{
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
            await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act & assert
            await SagaStore
                .Awaiting(x => x.Update(partitionKey, id, new SagaData<SagaData> { Data = new SagaData { Message = "updated" }, Etag = "etag" }))
                .Should()
                .ThrowAsync<SagaDataConflictException>();
        }

        [Test]
        public async Task Update_should_update_when_etag_match()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            var sagaData = await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act
            await SagaStore.Update(partitionKey, id, new SagaData<SagaData> { Data = new SagaData { Message = "updated" }, Etag = sagaData.Etag });
            //assert
            var data = await SagaStore.GetSaga<SagaData>(partitionKey, id);
            data.Data.Message.Should().Be("updated");
        }
    }
}
