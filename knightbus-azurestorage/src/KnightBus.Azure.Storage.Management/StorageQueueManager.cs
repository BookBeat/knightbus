using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using KnightBus.Core.Management;
using KnightBus.Core.PreProcessors;

namespace KnightBus.Azure.Storage.Management;

public class StorageQueueManager : IQueueManager
{
    private readonly IStorageBusConfiguration _configuration;
    private readonly IEnumerable<IMessagePreProcessor> _preProcessors;
    private readonly QueueServiceClient _client;

    public StorageQueueManager(IStorageBusConfiguration configuration, IEnumerable<IMessagePreProcessor> preProcessors)
    {
        _configuration = configuration;
        _preProcessors = preProcessors;
        _client = new QueueServiceClient(configuration.ConnectionString);
    }
    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = _client.GetQueuesAsync(cancellationToken: ct);
        var properties = new List<Task<QueueProperties>>();
        await foreach (var queue in queues)
        {
            if (queue.Name.EndsWith("-dl"))
                continue;
            properties.Add(Get(queue.Name, ct));
        }

        return await Task.WhenAll(properties);
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var qc = new StorageQueueClient(
            _configuration, _configuration.MessageSerializer, _preProcessors, path);
        var queueCount = 0;
        var dlCount = 0;

        await Task.WhenAll(
            qc.GetQueueCountAsync().ContinueWith(task => queueCount = task.Result, ct),
            SafeGetDeadLetterCount(qc).ContinueWith(task => dlCount = task.Result, ct)
        ).ConfigureAwait(false);


        return new QueueProperties(path, this, true)
        {
            ActiveMessageCount = queueCount,
            DeadLetterMessageCount = dlCount
        };
    }

    private static async Task<int> SafeGetDeadLetterCount(IStorageQueueClient qc)
    {
        try
        {
            var count = await qc.GetDeadLetterCountAsync().ConfigureAwait(false);
            return count;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return 0;
        }

    }

    public async Task Delete(string path, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _preProcessors, path);

        await qc.DeleteIfExistsAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _preProcessors, name);


        var messages = await qc.PeekMessagesAsync<DictionaryMessage>(count).ConfigureAwait(false);

        return messages.Select(m =>
        {
            m.Properties.TryGetValue("Error", out var error);
            return new QueueMessage(Encoding.UTF8.GetString(_configuration.MessageSerializer.Serialize(m.Message)),
                error ?? string.Empty, null, null, m.DequeueCount, m.BlobMessageId, m.Properties.AsReadOnly());
        }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string path, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _preProcessors, path);

        var messages = await qc.PeekDeadLettersAsync<DictionaryMessage>(count).ConfigureAwait(false);

        return messages.Select(m =>
        {
            m.Properties.TryGetValue("Error", out var error);
            return new QueueMessage(Encoding.UTF8.GetString(_configuration.MessageSerializer.Serialize(m.Message)),
                error ?? string.Empty, null, null, m.DequeueCount, m.BlobMessageId, m.Properties);
        }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(string path, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _preProcessors, path);

        var messages = new List<QueueMessage>();
        for (var i = 0; i < count; i++)
        {
            var message = await qc.ReceiveDeadLetterAsync<DictionaryMessage>().ConfigureAwait(false);
            if (message == null)
                break;
            messages.Add(
                new QueueMessage(
                    Encoding.UTF8.GetString(_configuration.MessageSerializer.Serialize(message.Message)),
                    message.Properties.TryGetValue("Error", out var error) ? error : string.Empty,
                    null,
                    null,
                    message.DequeueCount,
                    message.BlobMessageId,
                    message.Properties));
        }

        return messages;
    }

    public async Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _preProcessors, path);
        await qc.RequeueDeadLettersAsync<DictionaryMessage>(count, null).ConfigureAwait(false);
        return count;
    }
    public QueueType QueueType => QueueType.Queue;
}
