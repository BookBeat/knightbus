using System.Text;
using Azure;
using Azure.Storage.Queues;
using KnightBus.Azure.Storage;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;

namespace KnightBus.UI.Blazor.Providers.StorageBus;

public class StorageQueueManager : IQueueManager
{
    private readonly IStorageBusConfiguration _configuration;
    private readonly QueueServiceClient _client;
    private readonly AttachmentPreProcessor[] _attachmentPreProcessors = [];

    public StorageQueueManager(EnvironmentService environmentService, IServiceProvider provider, IMessageAttachmentProvider? attachmentProvider = null)
    {
        var configuration = provider.GetRequiredKeyedService<EnvironmentConfig>(environmentService.Get());
        _client = new QueueServiceClient(configuration.StorageBusConnectionString);

        _configuration = new StorageBusConfiguration
        {
            ConnectionString = configuration.StorageBusConnectionString
        };

        if (attachmentProvider != null)
            _attachmentPreProcessors = [new AttachmentPreProcessor(attachmentProvider)];
    }
    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues =  _client.GetQueuesAsync(cancellationToken: ct);
        var properties = new List<QueueProperties>();
        await foreach (var queue in queues)
        {
            if(!queue.Name.EndsWith("-dl"))
                continue;
            properties.Add(await Get(queue.Name, ct));
        }

        return properties;
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var qc = new StorageQueueClient(
            _configuration, _configuration.MessageSerializer, _attachmentPreProcessors, path);
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
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _attachmentPreProcessors, path);

        await qc.DeleteIfExistsAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _attachmentPreProcessors, name);

        var messages = await qc.GetMessagesAsync<FakeMessage>(count, null).ConfigureAwait(false);

        return messages.Select(m =>
        {
            m.Properties.TryGetValue("Error", out var error);
            return new QueueMessage(Encoding.UTF8.GetString(_configuration.MessageSerializer.Serialize(m.Message)),
                error ?? string.Empty, null, m.DequeueCount, m.BlobMessageId);
        }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _attachmentPreProcessors, name);

        var messages = await qc.PeekDeadLettersAsync<FakeMessage>(count).ConfigureAwait(false);

        return messages.Select(m =>
        {
            m.Properties.TryGetValue("Error", out var error);
            return new QueueMessage(Encoding.UTF8.GetString(_configuration.MessageSerializer.Serialize(m.Message)),
                error ?? string.Empty, null, m.DequeueCount, m.BlobMessageId);
        }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(string name, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _attachmentPreProcessors, name);

        var messages = new List<QueueMessage>();
        for (var i = 0; i < count; i++)
        {
            var message = await qc.ReceiveDeadLetterAsync<FakeMessage>().ConfigureAwait(false);
            if (message == null)
                break;
            messages.Add(
                new QueueMessage(
                    Encoding.UTF8.GetString(_configuration.MessageSerializer.Serialize(message.Message)),
                    message.Properties.TryGetValue("Error", out var error) ? error : string.Empty,
                    null,
                    message.DequeueCount,
                    message.BlobMessageId));
        }

        return messages;
    }

    public async Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _attachmentPreProcessors, name);
        await qc.RequeueDeadLettersAsync<FakeMessage>(count, null).ConfigureAwait(false);
        return count;
    }

    public string DisplayName => "Storage Queue";
    public QueueType QueueType => QueueType.Queue;
}
