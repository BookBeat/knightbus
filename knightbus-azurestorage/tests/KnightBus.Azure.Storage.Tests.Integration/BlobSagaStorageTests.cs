using KnightBus.Azure.Storage.Sagas;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration
{
    [TestFixture]
    public class BlobSagaStorageTests : SagaStoreTests
    {
        public override void Setup()
        {
            SagaStore = new BlobSagaStore("UseDevelopmentStorage=true");
        }
    }
}
