using System.Text;
using Azure.Storage.Queues;
using KnightBus.Azure.Storage;
using KnightBus.Newtonsoft;

namespace KnightBus.UI.Console.Providers.StorageBus;

public class StorageQueueManager : IQueueManager
{
    private readonly string _connectionString;
    private readonly QueueServiceClient _client;

    public StorageQueueManager(string connectionString)
    {
        _connectionString = connectionString;
        _client = new QueueServiceClient(connectionString);
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
        var config = new StorageBusConfiguration(_connectionString) { MessageEncoding = QueueMessageEncoding.Base64 };
        var qc = new StorageQueueClient(config, new NewtonsoftSerializer(), new BlobStorageMessageAttachmentProvider(config), path);
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

    public Task Delete(string path, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        var config = new StorageBusConfiguration(_connectionString) { MessageEncoding = QueueMessageEncoding.Base64 };
        var serializer = new NewtonsoftSerializer();
        var qc = new StorageQueueClient(config, serializer, new BlobStorageMessageAttachmentProvider(config), name);

        var messages = await qc.PeekDeadLettersAsync<FakeMessage>(10).ConfigureAwait(false);

        return messages.Select(m =>
        {
            m.Properties.TryGetValue("Error", out var error);
            return new QueueMessage(Encoding.UTF8.GetString(serializer.Serialize(m.Message)),
                error ?? string.Empty, m.InsertedOn);
        }).ToList();
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
