using System;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.Sagas.Exceptions;
using NUnit.Framework;

namespace KnightBus.SqlServer.Tests.Integration
{
    [TestFixture]
    public class SqlSagaStoreTests
    {
        [Test]
        public async Task Should_create_and_read_saga()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            //act
            await sagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //assert
            var saga = await sagaStore.GetSaga<SagaData>(partitionKey, id);
            saga.Message.Should().Be("yo");
        }

        [Test]
        public async Task Should_throw_when_create_and_saga_exists()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            await sagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act & assert
            sagaStore.Awaiting(x => x.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1))).Should().Throw<SagaAlreadyStartedException>();
        }

        [Test]
        public void Should_throw_when_get_and_saga_does_not_exist()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            //act & assert
            sagaStore.Awaiting(x => x.GetSaga<SagaData>(partitionKey, id)).Should().Throw<SagaNotFoundException>();
        }
        [Test]
        public async Task Complete_should_delete_the_saga()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            await sagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act
            await sagaStore.Complete(partitionKey, id);
            //assert
            sagaStore.Awaiting(x => x.GetSaga<SagaData>(partitionKey, id)).Should().Throw<SagaNotFoundException>();
        }

        [Test]
        public void Complete_should_throw_when_saga_not_found()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            //act & assert
            sagaStore.Awaiting(x => x.Complete(partitionKey, id)).Should().Throw<SagaNotFoundException>();
        }

        [Test]
        public async Task Update_should_update_the_saga()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            await sagaStore.Create(partitionKey, id, new SagaData { Message = "yo" }, TimeSpan.FromMinutes(1));
            //act
            await sagaStore.Update(partitionKey, id, new SagaData { Message = "updated" });
            //assert
            var data = await sagaStore.GetSaga<SagaData>(partitionKey, id);
            data.Message.Should().Be("updated");
        }

        [Test]
        public void Update_should_throw_when_saga_not_found()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            //act & assert
            sagaStore.Awaiting(x => x.Update(partitionKey, id, new SagaData())).Should().Throw<SagaNotFoundException>();
        }

        private class SagaData
        {
            public string Message { get; set; }
        }
    }
}
