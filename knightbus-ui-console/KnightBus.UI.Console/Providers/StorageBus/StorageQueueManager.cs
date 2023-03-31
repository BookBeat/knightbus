using System.Text;
using Azure.Storage.Queues;
using KnightBus.Azure.Storage;
using KnightBus.Core;

namespace KnightBus.UI.Console.Providers.StorageBus;

public class StorageQueueManager : IQueueManager
{
    private readonly IStorageBusConfiguration _configuration;
    private readonly IMessageAttachmentProvider _attachmentProvider;
    private readonly QueueServiceClient _client;

    public StorageQueueManager(IStorageBusConfiguration configuration, IMessageAttachmentProvider attachmentProvider = null)
    {
        _configuration = configuration;
        _attachmentProvider = attachmentProvider;
        _client = new QueueServiceClient(_configuration.ConnectionString);
    }
    public IEnumerable<QueueProperties> List(CancellationToken ct)
    {
        var queues = _client.GetQueues(cancellationToken: ct);
        foreach (var queue in queues.Where(q => !q.Name.EndsWith("-dl")))
        {
            yield return new QueueProperties(queue.Name, QueueType.Queue, this, false, false);
        }
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, new BlobStorageMessageAttachmentProvider(_configuration), path);
        var queueCount = 0;
        var dlCount = 0;

        await Task.WhenAll(
            qc.GetQueueCountAsync().ContinueWith(task => queueCount = task.Result, TaskContinuationOptions.OnlyOnRanToCompletion),
            qc.GetDeadLetterCountAsync().ContinueWith(task => dlCount = task.Result, TaskContinuationOptions.OnlyOnRanToCompletion)
        ).ConfigureAwait(false);


        return new QueueProperties(path, QueueType.Queue, this, false, true)
        {
            ActiveMessageCount = queueCount,
            DeadLetterMessageCount = dlCount
        };
    }

    public async Task Delete(string path, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _attachmentProvider, path);

        await qc.DeleteIfExistsAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        var qc = new StorageQueueClient(_configuration, _configuration.MessageSerializer, _attachmentProvider, name);

        var messages = await qc.PeekDeadLettersAsync<FakeMessage>(10).ConfigureAwait(false);

        return messages.Select(m =>
        {
            m.Properties.TryGetValue("Error", out var error);
            return new QueueMessage(Encoding.UTF8.GetString(_configuration.MessageSerializer.Serialize(m.Message)),
                error ?? string.Empty, m.InsertedOn);
        }).ToList();
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
