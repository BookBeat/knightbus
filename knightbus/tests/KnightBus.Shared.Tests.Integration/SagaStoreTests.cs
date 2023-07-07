using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using NUnit.Framework;

namespace KnightBus.Shared.Tests.Integration
{
    [TestFixture]
    public class SagaStoreTests
    {
        protected ISagaStore SagaStore { get; set; }

        protected class Data
        {
            public string Message { get; set; }
        }

        [SetUp]
        public virtual void Setup()
        {

        }

        [Test]
        public async Task Complete_should_delete_the_saga()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            var sagaData = new SagaData<Data> { Data = new Data { Message = "yo" } };
            await SagaStore.Create(partitionKey, id, sagaData.Data, TimeSpan.FromMinutes(1), CancellationToken.None);
            //act
            await SagaStore.Complete(partitionKey, id, sagaData, CancellationToken.None);
            //assert
            await SagaStore
                .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
                .Should()
                .ThrowAsync<SagaNotFoundException>();
        }

        [Test]
        public async Task Complete_should_throw_when_saga_not_found()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //act & assert
            await SagaStore
                .Awaiting(x => x.Complete(partitionKey, id, new SagaData<Data>(), CancellationToken.None))
                .Should()
                .ThrowAsync<SagaNotFoundException>();
        }

        [Test]
        public async Task Should_create_and_read_saga()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");

            //act
            await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
            //assert
            var saga = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
            saga.Data.Message.Should().Be("yo");
        }

        [Test]
        public async Task Should_not_throw_when_create_and_saga_expired()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMilliseconds(1), CancellationToken.None);
            await Task.Delay(2);
            //act
            var sagaData = await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
            //assert
            sagaData.Should().NotBe(null);

        }

        [Test]
        public async Task Should_throw_when_create_and_saga_exists()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
            //act & assert
            await SagaStore
                .Awaiting(x => x.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None))
                .Should()
                .ThrowAsync<SagaAlreadyStartedException>();
        }

        [Test]
        public async Task Should_throw_when_get_and_saga_does_not_exist()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //act & assert
            await SagaStore
                .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
                .Should()
                .ThrowAsync<SagaNotFoundException>();
        }

        [Test]
        public async Task Should_throw_when_get_and_saga_expired()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMilliseconds(1), CancellationToken.None);
            await Task.Delay(2);
            //act & assert
            await SagaStore
                .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
                .Should()
                .ThrowAsync<SagaNotFoundException>();
        }

        [Test]
        public async Task Update_should_throw_when_saga_not_found()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //act & assert
            await SagaStore
                .Awaiting(x => x.Update(partitionKey, id, new SagaData<Data>(), CancellationToken.None))
                .Should()
                .ThrowAsync<SagaNotFoundException>();
        }

        [Test]
        public async Task Update_should_update_the_saga()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new Data { Message = "yo" }, TimeSpan.FromMinutes(1), CancellationToken.None);
            //act
            await SagaStore.Update(partitionKey, id, new SagaData<Data> { Data = new Data { Message = "updated" }}, CancellationToken.None);
            //assert
            var data = await SagaStore.GetSaga<Data>(partitionKey, id, CancellationToken.None);
            data.Data.Message.Should().Be("updated");
        }

        [Test]
        public async Task Delete_should_delete_the_saga()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            var sagaData = new SagaData<Data> { Data = new Data { Message = "yo" } };
            await SagaStore.Create(partitionKey, id, sagaData.Data, TimeSpan.FromMinutes(1), CancellationToken.None);
            //act
            await SagaStore.Delete(partitionKey, id, CancellationToken.None);
            //assert
            await SagaStore
                .Awaiting(x => x.GetSaga<Data>(partitionKey, id, CancellationToken.None))
                .Should()
                .ThrowAsync<SagaNotFoundException>();
        }
    }
}
