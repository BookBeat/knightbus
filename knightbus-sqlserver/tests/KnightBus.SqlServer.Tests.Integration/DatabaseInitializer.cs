using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace KnightBus.SqlServer.Tests.Integration
{
    [SetUpFixture]
    public class DatabaseInitializer
    {
        private const string DatabaseName = "KnightBus";
        public static readonly string ConnectionString = $@"Server=(local)\SQL2019;Database={DatabaseName};User ID=sa;Password=Password12!";
        private const string AdminConnectionString = @"Server=(local)\SQL2019;Database=master;User ID=sa;Password=Password12!";

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