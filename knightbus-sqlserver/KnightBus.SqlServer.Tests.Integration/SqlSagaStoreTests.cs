using System;
using System.Threading.Tasks;
using KnightBus.Core;
using NUnit.Framework;

namespace KnightBus.SqlServer.Tests.Integration
{
    [TestFixture]
    public class SqlSagaStoreTests
    {
        [Test]
        public async Task Should_create_saga()
        {
            var partitionKey = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid().ToString("N");
            //arrange
            var sagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString, new JsonMessageSerializer());
            //act
            await sagaStore.Create(partitionKey, id, new SagaData {Message = "yo"});
            //assert
        }

        private class SagaData
        {
            public string Message { get; set; } 
        }
    }
}
