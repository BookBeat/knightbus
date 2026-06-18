using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Management;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace KnightBus.LavinMQ.Management;

/// <summary>
/// Queue management for the LavinMQ transport. Message-level operations use AMQP over the shared
/// connection; only <see cref="List"/> uses the LavinMQ HTTP management API (port 15672), since
/// enumerating queues is not possible over AMQP. The management endpoint and credentials are derived
/// from the AMQP connection string (host + 15672, same user info).
/// </summary>
public class LavinMQQueueManager : IQueueManager, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IConnection _connection;
    private readonly HttpClient _httpClient;
    private readonly string _vhostSegment;

    public LavinMQQueueManager(ILavinMQConfiguration configuration, IConnection connection)
    {
        _connection = connection;

        var uri = new Uri(configuration.ConnectionString);
        var vhost =
            string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
                ? "/"
                : Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        _vhostSegment = Uri.EscapeDataString(vhost);

        var managementBaseAddress = string.IsNullOrEmpty(configuration.ManagementApiUrl)
            ? new UriBuilder("http", uri.Host, 15672).Uri
            : new Uri(configuration.ManagementApiUrl);
        _httpClient = new HttpClient { BaseAddress = managementBaseAddress };
        var userInfo = string.IsNullOrEmpty(uri.UserInfo)
            ? "guest:guest"
            : Uri.UnescapeDataString(uri.UserInfo);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(userInfo))
        );
    }

    public QueueType QueueType => QueueType.Queue;

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        using var response = await _httpClient
            .GetAsync($"/api/queues/{_vhostSegment}", ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var queues =
            await JsonSerializer
                .DeserializeAsync<List<QueueInfo>>(stream, JsonOptions, ct)
                .ConfigureAwait(false) ?? new List<QueueInfo>();

        return queues
            .Select(q => q.Name)
            .Where(IsPrimaryQueue)
            .Select(name => new QueueProperties(name, this, false, QueueType.Queue))
            .ToList();
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var active = await GetMessageCountAsync(path, ct).ConfigureAwait(false);
        var dead = await GetMessageCountAsync(LavinMQQueueConventions.DeadLetterQueueName(path), ct)
            .ConfigureAwait(false);

        return new QueueProperties(path, this, true, QueueType.Queue)
        {
            ActiveMessageCount = active,
            DeadLetterMessageCount = dead,
        };
    }

    public async Task Delete(string path, CancellationToken ct)
    {
        await using var channel = await _connection
            .CreateChannelAsync(cancellationToken: ct)
            .ConfigureAwait(false);
        try
        {
            await channel
                .QueueDeleteAsync(path, ifUnused: false, ifEmpty: false, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (OperationInterruptedException)
        {
            // Queue does not exist - delete is idempotent
        }
    }

    public Task<IReadOnlyList<QueueMessage>> Peek(string path, int count, CancellationToken ct) =>
        PeekQueueAsync(path, count, ct);

    public Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string path,
        int count,
        CancellationToken ct
    ) => PeekQueueAsync(LavinMQQueueConventions.DeadLetterQueueName(path), count, ct);

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        var deadLetterQueue = LavinMQQueueConventions.DeadLetterQueueName(path);
        await using var channel = await _connection
            .CreateChannelAsync(cancellationToken: ct)
            .ConfigureAwait(false);

        var messages = new List<QueueMessage>();
        for (var i = 0; i < count; i++)
        {
            // autoAck removes the message from the dead-letter queue
            var result = await channel
                .BasicGetAsync(deadLetterQueue, autoAck: true, ct)
                .ConfigureAwait(false);
            if (result is null)
                break;
            messages.Add(ToQueueMessage(result));
        }

        return messages;
    }

    public async Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {
        var deadLetterQueue = LavinMQQueueConventions.DeadLetterQueueName(path);
        await using var channel = await _connection
            .CreateChannelAsync(cancellationToken: ct)
            .ConfigureAwait(false);

        var moved = 0;
        for (var i = 0; i < count; i++)
        {
            var result = await channel
                .BasicGetAsync(deadLetterQueue, autoAck: false, ct)
                .ConfigureAwait(false);
            if (result is null)
                break;

            // Republish to the original queue via the default exchange, then ack the dead letter.
            var properties = new BasicProperties(result.BasicProperties);
            await channel
                .BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: path,
                    mandatory: false,
                    basicProperties: properties,
                    body: result.Body,
                    cancellationToken: ct
                )
                .ConfigureAwait(false);
            await channel
                .BasicAckAsync(result.DeliveryTag, multiple: false, ct)
                .ConfigureAwait(false);
            moved++;
        }

        return moved;
    }

    public Task<IReadOnlyList<QueueMessage>> PeekScheduled(
        string name,
        int count,
        CancellationToken ct
    ) => throw new NotSupportedException();

    private async Task<IReadOnlyList<QueueMessage>> PeekQueueAsync(
        string queue,
        int count,
        CancellationToken ct
    )
    {
        await using var channel = await _connection
            .CreateChannelAsync(cancellationToken: ct)
            .ConfigureAwait(false);

        var messages = new List<QueueMessage>();
        for (var i = 0; i < count; i++)
        {
            // autoAck:false and never acking -> closing the channel requeues the messages (non-destructive peek)
            var result = await channel
                .BasicGetAsync(queue, autoAck: false, ct)
                .ConfigureAwait(false);
            if (result is null)
                break;
            messages.Add(ToQueueMessage(result));
        }

        return messages;
    }

    private async Task<long> GetMessageCountAsync(string queue, CancellationToken ct)
    {
        try
        {
            await using var channel = await _connection
                .CreateChannelAsync(cancellationToken: ct)
                .ConfigureAwait(false);
            var ok = await channel.QueueDeclarePassiveAsync(queue, ct).ConfigureAwait(false);
            return ok.MessageCount;
        }
        catch (OperationInterruptedException)
        {
            return 0;
        }
    }

    private static QueueMessage ToQueueMessage(BasicGetResult result)
    {
        var properties = result.BasicProperties;
        var body = Encoding.UTF8.GetString(result.Body.Span);
        DateTimeOffset? time = properties.IsTimestampPresent()
            ? DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime)
            : null;
        var messageId = properties.IsMessageIdPresent() ? properties.MessageId : string.Empty;

        return new QueueMessage(
            body,
            null,
            time,
            null,
            result.Redelivered ? 2 : 1,
            messageId,
            ReadHeaders(properties)
        );
    }

    private static IReadOnlyDictionary<string, string> ReadHeaders(
        IReadOnlyBasicProperties properties
    )
    {
        var headers = new Dictionary<string, string>();
        if (properties.Headers == null)
            return headers;

        foreach (var header in properties.Headers)
        {
            headers[header.Key] = header.Value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                null => string.Empty,
                _ => header.Value.ToString() ?? string.Empty,
            };
        }

        return headers;
    }

    private static bool IsPrimaryQueue(string name) =>
        !string.IsNullOrEmpty(name)
        && !name.EndsWith(".dl", StringComparison.Ordinal)
        && !name.StartsWith("amq.", StringComparison.Ordinal);

    public void Dispose() => _httpClient.Dispose();

    private sealed record QueueInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }
}
