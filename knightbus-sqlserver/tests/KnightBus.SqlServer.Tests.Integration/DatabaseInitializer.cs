using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace KnightBus.SqlServer.Tests.Integration
{
    [SetUpFixture]
    public class DatabaseInitializer
    {
        private const string DatabaseName = "KnightBus";
        public static readonly string ConnectionString = $@"Server=localhost;Database={DatabaseName};User ID=sa;Password=Password12!;Encrypt=True;TrustServerCertificate=True;";
        private const string AdminConnectionString = @"Server=localhost;Database=master;User ID=sa;Password=Password12!;Encrypt=True;TrustServerCertificate=True;";

        [OneTimeSetUp]
        public void Setup()
        {
            using (var connection = new SqlConnection(AdminConnectionString))
            {
                connection.Open();
                var sql = $@"IF DB_ID (N'{DatabaseName}') IS NOT NULL
                            DROP DATABASE {DatabaseName};
                            CREATE DATABASE {DatabaseName};";
                var command = new SqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
        }
    }
}
