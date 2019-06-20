using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;

namespace KnightBus.SqlServer
{
    public class SqlServerSagaStore : ISagaStore
    {
        private readonly string _connectionString;
        private readonly IMessageSerializer _serializer;
        private const string _tableName = "Sagas";
        private const string _schema = "KnightBus";

        public SqlServerSagaStore(string connectionString, IMessageSerializer serializer)
        {
            _connectionString = connectionString;
            _serializer = serializer;
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
            return command;
        }

        public async Task<T> GetSaga<T>(string partitionKey, string id)
        {
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"SELECT Json FROM {_schema}.{_tableName} WHERE PartitionKey = @PartitionKey AND Id = @Id";
                var command = GetCommandWithParameters(sql, connection, partitionKey, id);
                var result = await command.ExecuteReaderAsync(CommandBehavior.SingleResult).ConfigureAwait(false);

                if (!result.HasRows) throw new SagaNotFoundException(partitionKey, id);

                var json = result.GetString(0);
                return _serializer.Deserialize<T>(json);
            }
        }

        public async Task<T> Create<T>(string partitionKey, string id, T sagaData)
        {
            var json = _serializer.Serialize(sagaData);
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"INSERT INTO {_schema}.{_tableName} (PartitionKey, Id, Json) VALUES (@PartitionKey, @Id, @Json)";
                var command = GetCommandWithParameters(sql, connection, partitionKey, id);
                command.Parameters.AddWithValue("@Json", json);
                await command.ExecuteNonQueryAsync();
            }

            return sagaData;
        }

        public async Task Update<T>(string partitionKey, string id, T sagaData)
        {
            var json = _serializer.Serialize(sagaData);
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"UPDATE {_schema}.{_tableName} SET Json = @Json WHERE PartitionKey = @PartitionKey AND Id = @Id";
                var command = GetCommandWithParameters(sql, connection, partitionKey, id);
                command.Parameters.AddWithValue("@Json", json);
                var result = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                if(result == 0) throw new SagaNotFoundException(partitionKey, id);
            }
        }

        public async Task Complete(string partitionKey, string id)
        {
            using (var connection = await GetConnection().ConfigureAwait(false))
            {
                var sql = $@"DELETE FROM {_schema}.{_tableName} WHERE PartitionKey = @PartitionKey AND Id = @Id";
                var command = GetCommandWithParameters(sql, connection, partitionKey, id);

                var result = await command.ExecuteNonQueryAsync();
                if (result == 0) throw new SagaNotFoundException(partitionKey, id);
            }
        }
    }
}
