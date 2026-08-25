using KnightBus.PostgreSql.Sagas;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;
using static KnightBus.PostgreSql.Tests.Integration.PostgresSetup;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresSagaStoreTests : SagaStoreTests
{
    public override void Setup()
    {
        SagaStore = new PostgresSagaStore(
            DataSource,
            new PostgresConfiguration(DataSource.ConnectionString)
        );
    }
}
