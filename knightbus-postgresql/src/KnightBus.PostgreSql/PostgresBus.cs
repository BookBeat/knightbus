using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;
using NpgsqlTypes;

namespace KnightBus.PostgreSql;

public interface IPostgresBus
{
    Task SendAsync<T>(T message) where T : IPostgresCommand;
    Task SendAsync<T>(IEnumerable<T> messages) where T : IPostgresCommand;
}

public class PostgresBus : IPostgresBus
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;

    public PostgresBus(NpgsqlDataSource npgsqlDataSource, IPostgresConfiguration postgresConfiguration)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = postgresConfiguration.MessageSerializer;
    }

    public Task SendAsync<T>(T message) where T : IPostgresCommand
    {
        return SendAsync([message]);
    }

    public async Task SendAsync<T>(IEnumerable<T> messages) where T : IPostgresCommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        var parameters = new List<NpgsqlParameter>();
        var values = "";
        var messagesList = messages.ToList();

        for (int i = 0; i < messagesList.Count; i++)
        {
            values += $"((now()), (${i+1})),";
            var mBody = _serializer.Serialize(messagesList[i]);
            parameters.Add(new NpgsqlParameter { Value = mBody, NpgsqlDbType = NpgsqlDbType.Jsonb });
        }

        values = values.TrimEnd(',');

        await using var command = _npgsqlDataSource.CreateCommand(@$"
INSERT INTO knightbus.q_{queueName} (visibility_timeout, message)
VALUES {values}
");
        foreach (var p in parameters)
        {
            command.Parameters.Add(p);
        }

        await command.ExecuteNonQueryAsync();
    }
}
