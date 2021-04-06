using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using System.Threading.Tasks;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;

namespace KnightBus.SqlServer
{
    public class SqlServerSagaStore : ISagaStore
    {
        private readonly string _connectionString;
        private const string _tableName = "Sagastore";
        private const string _schema = "dbo";

        public SqlServerSagaStore(string connectionString)
        {
            _connectionString = connectionString;
        }



        private async Task<SqlConnection> GetConnection()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        }

        private SqlCommand GetCommandWithParameters(string sql, SqlConnection connection, string partitionKey, string id)
        {
            var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PartitionKey", partitionKey);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@UtcNow", DateTime.UtcNow);
            return command;
        }

        private async Task CreateSagaTable(SqlConnection connection)
        {
            var ddl = $@"IF NOT (EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES 
                         WHERE TABLE_SCHEMA = '{_schema}' AND  TABLE_NAME = '{_tableName}'))
                         BEGIN
                            CREATE TABLE {_schema}.{_tableName} 
                            (
                                PartitionKey NVARCHAR(50),
                                Id NVARCHAR(50),
                                Json NVARCHAR(4000),
                                Expiration DATETIME,
                                PRIMARY KEY (PartitionKey, Id)
                            )
                         END";
            var command = new SqlCommand(ddl, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> GetSaga<T>(string partitionKey, string id)
        {
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"SELECT Json FROM {_schema}.{_tableName} WHERE PartitionKey = @PartitionKey AND Id = @Id AND Expiration > @UtcNow";
                var command = GetCommandWithParameters(sql, connection, partitionKey, id);
                try
                {
                    var result = await command.ExecuteReaderAsync(CommandBehavior.SingleResult).ConfigureAwait(false);

                    if (!result.HasRows) throw new SagaNotFoundException(partitionKey, id);

                    await result.ReadAsync().ConfigureAwait(false);
                    var json = result.GetString(0);
                    return JsonSerializer.Deserialize<T>(json);
                }
                catch (SqlException e) when (e.Number == 208)
                {
                    await CreateSagaTable(connection);
                    return await GetSaga<T>(partitionKey, id).ConfigureAwait(false);
                }
            }
        }

        public async Task<T> Create<T>(string partitionKey, string id, T sagaData, TimeSpan ttl)
        {
            var json = JsonSerializer.Serialize(sagaData);
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"
DECLARE @ExistingExpiration DATETIME
IF EXISTS(SELECT Id FROM {_schema}.{_tableName} WHERE PartitionKey = @PartitionKey AND Id = @Id)
BEGIN
    SELECT @ExistingExpiration = Expiration FROM {_schema}.{_tableName} WHERE PartitionKey = @PartitionKey AND Id = @Id
    IF @ExistingExpiration <= @UtcNow
    BEGIN
        DELETE FROM {_schema}.{_tableName} WHERE PartitionKey = @PartitionKey AND Id = @Id
    END
END

INSERT INTO {_schema}.{_tableName} (PartitionKey, Id, Json, Expiration) VALUES (@PartitionKey, @Id, @Json, @Expiration)";

                var command = GetCommandWithParameters(sql, connection, partitionKey, id);
                command.Parameters.AddWithValue("@Json", json);
                command.Parameters.AddWithValue("@Expiration", DateTime.UtcNow.Add(ttl));
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (SqlException e) when (e.Number == 208)
                {
                    await CreateSagaTable(connection);
                    return await Create(partitionKey, id, sagaData, ttl).ConfigureAwait(false);
                }
                catch (SqlException e) when (e.Number == 2627)
                {
                    throw new SagaAlreadyStartedException(partitionKey, id);
                }
            }

            return sagaData;
        }

        public async Task Update<T>(string partitionKey, string id, T sagaData)
        {
            var json = JsonSerializer.Serialize(sagaData);
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"UPDATE {_schema}.{_tableName} SET Json = @Json WHERE PartitionKey = @PartitionKey AND Id = @Id";
                var command = GetCommandWithParameters(sql, connection, partitionKey, id);
                command.Parameters.AddWithValue("@Json", json);
                try
                {
                    var result = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    if (result == 0) throw new SagaNotFoundException(partitionKey, id);
                }
                catch (SqlException e) when (e.Number == 208)
                {
                    await CreateSagaTable(connection);
                    await Update(partitionKey, id, sagaData).ConfigureAwait(false);
                }
            }
        }

        public async Task Complete(string partitionKey, string id)
        {
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"DELETE FROM {_schema}.{_tableName} WHERE PartitionKey = @PartitionKey AND Id = @Id";
                var command = GetCommandWithParameters(sql, connection, partitionKey, id);

                try
                {
                    var result = await command.ExecuteNonQueryAsync();
                    if (result == 0) throw new SagaNotFoundException(partitionKey, id);
                }
                catch (SqlException e) when (e.Number == 208)
                {
                    await CreateSagaTable(connection);
                    await Complete(partitionKey, id).ConfigureAwait(false);
                }
            }
        }
    }
}
