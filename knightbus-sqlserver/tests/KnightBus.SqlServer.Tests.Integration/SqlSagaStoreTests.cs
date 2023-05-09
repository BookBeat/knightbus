using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.SqlServer.Tests.Integration
{
    [TestFixture]
    public class SqlSagaStoreTests : SagaStoreTests
    {
        public override void Setup()
        {
            SagaStore = new SqlServerSagaStore(DatabaseInitializer.ConnectionString);
        }
    }
}
