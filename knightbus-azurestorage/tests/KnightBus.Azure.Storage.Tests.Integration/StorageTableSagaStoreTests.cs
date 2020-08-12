using KnightBus.Azure.Storage.Sagas;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration
{
    [TestFixture]
    public class StorageTableSagaStoreTests : SagaStoreTests
    {
        public override void Setup()
        {
            SagaStore = new StorageTableSagaStore("UseDevelopmentStorage=true");
        }
    }
}