using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public interface IPostgresBus
{
    Task SendAsync<T>(T message) where T : IPostgresCommand;
    Task SendAsync<T>(IEnumerable<T> messages) where T : IPostgresCommand;
    Task ScheduleAsync<T>(T message, TimeSpan delay) where T : IPostgresCommand;
    Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay) where T : IPostgresCommand;
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
        await SendAsyncInternal(messages, null);
    }

    public Task ScheduleAsync<T>(T message, TimeSpan delay) where T : IPostgresCommand
    {
        return ScheduleAsync([message], delay);
    }

    public Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay) where T : IPostgresCommand
    {
        return SendAsyncInternal(messages, delay);
    }

    private async Task SendAsyncInternal<T>(IEnumerable<T> messages, TimeSpan? delay) where T : IPostgresCommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        var messagesList = messages.ToList();

        await using var connection = await _npgsqlDataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(null, connection);

        /*
         * Build a cmd text with multirow VALUES syntax and parameter placeholders
         * INSERT INTO queue (visibility_timeout, message) VALUES
         * ('now() + optional delay', ($1)),
         * ('now() + optional delay', ($2)),
         * ('now() + optional delay', ($3));
         */
        var values = "";
        for (int i = 0; i < messagesList.Count; i++)
        {
            // TODO: check if 0 epoch is faster vs now()
            // select extract(epoch from now());
            values += $"((now() + interval '{delay?.TotalSeconds ?? 0} seconds'), (${i+1})),";
            var mBody = _serializer.Serialize(messagesList[i]);
            command.Parameters.Add(new NpgsqlParameter { Value = mBody, NpgsqlDbType = NpgsqlDbType.Jsonb });
        }
        values = values.TrimEnd(',');

        command.CommandText = @$"
INSERT INTO {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message)
VALUES {values};
";
        await command.PrepareAsync();
        await command.ExecuteNonQueryAsync();
    }
}
