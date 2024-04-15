using System.Text;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public interface IPostgresBus
{
    Task SendAsync<T>(T message, CancellationToken ct) where T : IPostgresCommand;
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresCommand;
    Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand;
    Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand;
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

    public Task SendAsync<T>(T message, CancellationToken ct) where T : IPostgresCommand
    {
        return SendAsync([message], ct);
    }

    public async Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresCommand
    {
        await SendAsyncInternal(messages, null, ct);
    }

    public Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand
    {
        return ScheduleAsync([message], delay, ct);
    }

    public Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand
    {
        return SendAsyncInternal(messages, delay, ct);
    }

    private async Task SendAsyncInternal<T>(IEnumerable<T> messages, TimeSpan? delay, CancellationToken ct) where T : IPostgresCommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        var messagesList = messages.ToList();

        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(null, connection);

        /*
         * Build a cmd text with multirow VALUES syntax and parameter placeholders
         * INSERT INTO queue (visibility_timeout, message) VALUES
         * ('now() + optional delay', ($1)),
         * ('now() + optional delay', ($2)),
         * ('now() + optional delay', ($3));
         */
        var values = new StringBuilder();
        for (int i = 0; i < messagesList.Count; i++)
        {
            values.AppendFormat("((now() + interval '{0} seconds'), (${1})),", delay?.TotalSeconds ?? 0, i + 1);
            var mBody = _serializer.Serialize(messagesList[i]);
            command.Parameters.Add(new NpgsqlParameter { Value = mBody, NpgsqlDbType = NpgsqlDbType.Jsonb });
        }
        var stringValues = values.ToString().TrimEnd(',');

        command.CommandText = @$"
INSERT INTO {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message)
VALUES {stringValues};
";
        await command.PrepareAsync(ct);
        await command.ExecuteNonQueryAsync(ct);
    }
}
