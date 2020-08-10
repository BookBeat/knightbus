using System;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using NUnit.Framework;

namespace KnightBus.Shared.Tests.Integration
{
    [TestFixture]
    public class SagaStoreTests
    {
        protected ISagaStore SagaStore { get; set; }

        private class SagaData
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
            await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act
            await SagaStore.Complete(partitionKey, id);
            //assert
            SagaStore.Awaiting(x => x.GetSaga<SagaData>(partitionKey, id)).Should().Throw<SagaNotFoundException>();
        }

        [Test]
        public void Complete_should_throw_when_saga_not_found()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //act & assert
            SagaStore.Awaiting(x => x.Complete(partitionKey, id)).Should().Throw<SagaNotFoundException>();
        }

        [Test]
        public async Task Should_create_and_read_saga()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");

            //act
            await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //assert
            var saga = await SagaStore.GetSaga<SagaData>(partitionKey, id);
            saga.Message.Should().Be("yo");
        }

        [Test]
        public async Task Should_not_throw_when_create_and_saga_expired()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMilliseconds(1));
            await Task.Delay(2);
            //act & assert
            SagaStore.Awaiting(x => x.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1)))
                .Should().NotThrow<SagaAlreadyStartedException>();
        }

        [Test]
        public async Task Should_throw_when_create_and_saga_exists()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act & assert
            SagaStore.Awaiting(x => x.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1)))
                .Should().Throw<SagaAlreadyStartedException>();
        }

        [Test]
        public void Should_throw_when_get_and_saga_does_not_exist()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //act & assert
            SagaStore.Awaiting(x => x.GetSaga<SagaData>(partitionKey, id)).Should().Throw<SagaNotFoundException>();
        }

        [Test]
        public async Task Should_throw_when_get_and_saga_expired()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMilliseconds(1));
            await Task.Delay(2);
            //act & assert
            SagaStore.Awaiting(x => x.GetSaga<SagaData>(partitionKey, id)).Should().Throw<SagaNotFoundException>();
        }

        [Test]
        public void Update_should_throw_when_saga_not_found()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //act & assert
            SagaStore.Awaiting(x => x.Update(partitionKey, id, new SagaData())).Should().Throw<SagaNotFoundException>();
        }

        [Test]
        public async Task Update_should_update_the_saga()
        {
            //arrange
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            await SagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act
            await SagaStore.Update(partitionKey, id, new SagaData { Message = "updated" });
            //assert
            var data = await SagaStore.GetSaga<SagaData>(partitionKey, id);
            data.Message.Should().Be("updated");
        }
    }
}