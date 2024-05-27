using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Messages;
using Npgsql;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql.Sagas;

public class PostgresSagaStore : ISagaStore
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;

    private const string Table = "sagas";

    public PostgresSagaStore(NpgsqlDataSource npgsqlDataSource, IPostgresConfiguration configuration)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = configuration.MessageSerializer;
    }

    private async Task CreateSagaTable()
    {
        const string query = $"""
                              CREATE SCHEMA IF NOT EXISTS {SchemaName};
                              CREATE TABLE IF NOT EXISTS {SchemaName}.{Table} (
                                  partition_key TEXT NOT NULL,
                                  id TEXT NOT NULL,
                                  data BYTEA NOT NULL,
                                  expiration TIMESTAMP WITH TIME ZONE NOT NULL,
                                  PRIMARY KEY (partition_key, id)
                              );
                              """;
        await using var command = _npgsqlDataSource.CreateCommand(query);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    public async Task<SagaData<T>> GetSaga<T>(string partitionKey, string id, CancellationToken ct)
    {
        const string query =
            $"SELECT data FROM {SchemaName}.{Table} WHERE partition_key = $1 AND id = $2 AND expiration > $3";
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partitionKey });
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });
        command.Parameters.Add(new NpgsqlParameter<DateTime> { TypedValue = DateTime.UtcNow });

        try
        {
            await command.PrepareAsync(ct);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) throw new SagaNotFoundException(partitionKey, id);

            var bytes = await reader.GetFieldValueAsync<ReadOnlyMemory<byte>>(0, ct).ConfigureAwait(false);
            var data = _serializer.Deserialize<T>(bytes);
            return new SagaData<T> { Data = data };
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            await CreateSagaTable();
            return await GetSaga<T>(partitionKey, id, ct);
        }
    }

    public async Task<SagaData<T>> Create<T>(string partitionKey, string id, T sagaData, TimeSpan ttl,
        CancellationToken ct)
    {
        const string query = $"""
                              INSERT INTO {SchemaName}.{Table} (partition_key, id, data, expiration) VALUES ($1, $2, $3, $4)
                              ON CONFLICT (partition_key, id) DO UPDATE SET
                                  data = EXCLUDED.data,
                                  expiration = EXCLUDED.expiration
                              WHERE {SchemaName}.{Table}.expiration < $5;
                              """;
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(query, connection);

        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partitionKey });
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });
        command.Parameters.Add(new NpgsqlParameter<byte[]> { TypedValue = _serializer.Serialize<T>(sagaData) });
        command.Parameters.Add(new NpgsqlParameter<DateTime> { TypedValue = DateTime.UtcNow.Add(ttl) });
        command.Parameters.Add(new NpgsqlParameter<DateTime> { TypedValue = DateTime.UtcNow });

        try
        {
            await command.PrepareAsync(ct);
            var affectedRows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            if (affectedRows != 1) throw new SagaAlreadyStartedException(partitionKey, id);
            return new SagaData<T> { Data = sagaData };
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            await CreateSagaTable();
            return await Create(partitionKey, id, sagaData, ttl, ct);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new SagaAlreadyStartedException(partitionKey, id);
        }
    }

    public async Task Update<T>(string partitionKey, string id, SagaData<T> sagaData, CancellationToken ct)
    {
        const string query = $"UPDATE {SchemaName}.{Table} SET data = $1 WHERE partition_key = $2 AND id = $3";

        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(query, connection);
        var data = _serializer.Serialize(sagaData.Data);
        command.Parameters.Add(new NpgsqlParameter<byte[]> { TypedValue = data });
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partitionKey });
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });

        try
        {
            await command.PrepareAsync(ct);
            var affectedRows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (affectedRows != 1) throw new SagaNotFoundException(partitionKey, id);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            await CreateSagaTable();
            await Update(partitionKey, id, sagaData, ct);
        }
    }

    public async Task Complete<T>(string partitionKey, string id, SagaData<T> sagaData, CancellationToken ct)
    {
        const string query = $"DELETE FROM {SchemaName}.{Table} WHERE partition_key = $1 AND id = $2";
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partitionKey });
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });

        try
        {
            await command.PrepareAsync(ct);
            var affectedRows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (affectedRows != 1) throw new SagaNotFoundException(partitionKey, id);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            await CreateSagaTable();
            await Complete(partitionKey, id, sagaData, ct);
        }
    }

    public async Task Delete(string partitionKey, string id, CancellationToken ct)
    {
        const string query = $"DELETE FROM {SchemaName}.{Table} WHERE partition_key = $1 AND id = $2";
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partitionKey });
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });

        try
        {
            await command.PrepareAsync(ct);
            var affectedRows = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (affectedRows != 1) throw new SagaNotFoundException(partitionKey, id);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            await CreateSagaTable();
            await Delete(partitionKey, id, ct);
        }
    }
}
