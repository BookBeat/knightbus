using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Management;

namespace KnightBus.Redis.Management;

public class RedisQueueManager : IQueueManager, IQueueMessageAttachmentProvider
{
    private readonly IRedisManagementClient _managementClient;
    private readonly IRedisConfiguration _configuration;
    private readonly RedisAttachmentProvider _attachmentProvider;

    public RedisQueueManager(
        IRedisManagementClient managementClient,
        IRedisConfiguration configuration,
        RedisAttachmentProvider attachmentProvider
    )
    {
        _managementClient = managementClient;
        _configuration = configuration;
        _attachmentProvider = attachmentProvider;
    }

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = await _managementClient.ListQueues();
        return queues.Select(queue => new QueueProperties(queue, this, false, QueueType.Queue));
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var messageCount = await _managementClient.GetMessageCount<DictionaryMessage>(path);
        var deadLetterCount = await _managementClient.GetDeadletterMessageCount<DictionaryMessage>(
            path
        );

        return new QueueProperties(path, this, true, QueueType.Queue)
        {
            ActiveMessageCount = messageCount,
            DeadLetterMessageCount = deadLetterCount,
        };
    }

    public Task Delete(string path, CancellationToken ct)
    {
        return _managementClient.DeleteQueueAsync<DictionaryMessage>(path);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(
        string path,
        int count,
        CancellationToken ct
    )
    {
        var redisMessages = _managementClient.PeekMessagesAsync<DictionaryMessage>(path, count);
        var messages = new List<QueueMessage>();
        await foreach (var message in redisMessages)
        {
            var body = Encoding.UTF8.GetString(
                _configuration.MessageSerializer.Serialize(message.Message)
            );
            messages.Add(
                new QueueMessage(
                    body,
                    message.Error,
                    message.LastProcessed,
                    null,
                    message.DeliveryCount,
                    message.Id,
                    message.HashEntries.AsReadOnly()
                )
            );
        }

        return messages;
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        var deadLetters = _managementClient.PeekDeadlettersAsync<DictionaryMessage>(path, count);
        var messages = new List<QueueMessage>();
        await foreach (var deadLetter in deadLetters)
        {
            var body = Encoding.UTF8.GetString(
                _configuration.MessageSerializer.Serialize(deadLetter.Message.Body)
            );
            messages.Add(
                new QueueMessage(
                    body,
                    deadLetter.Error,
                    deadLetter.LastProcessed,
                    null,
                    deadLetter.DeliveryCount,
                    deadLetter.Message.Id,
                    deadLetter.HashEntries.AsReadOnly()
                )
            );
        }

        return messages;
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        var deadLetters = _managementClient.ReadDeadlettersAsync<DictionaryMessage>(path, count);
        var messages = new List<QueueMessage>();
        await foreach (var deadLetter in deadLetters)
        {
            var body = Encoding.UTF8.GetString(
                _configuration.MessageSerializer.Serialize(deadLetter.Message.Body)
            );
            messages.Add(
                new QueueMessage(
                    body,
                    deadLetter.Error,
                    deadLetter.LastProcessed,
                    null,
                    deadLetter.DeliveryCount,
                    deadLetter.Message.Id,
                    deadLetter.HashEntries.AsReadOnly()
                )
            );
        }

        return messages;
    }

    public Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {
        return _managementClient.RequeueDeadlettersAsync<DictionaryMessage>(path, count);
    }

    public QueueType QueueType => QueueType.Queue;

    public async Task<QueueMessageAttachment> GetAttachment(
        string queue,
        Dictionary<string, string> messageProperties,
        CancellationToken cancellationToken
    )
    {
        var attachmentId = AttachmentUtility.GetAttachmentIds(messageProperties).FirstOrDefault();
        if (string.IsNullOrEmpty(attachmentId))
            return null;

        var attachment = await _attachmentProvider.GetAttachmentAsync(
            queue,
            attachmentId,
            cancellationToken
        );

        if (attachment is null)
            return null;

        return new QueueMessageAttachment(
            attachment.Stream,
            attachment.ContentType,
            attachment.Filename,
            attachment.Length
        );
    }

    public bool HasAttachment(Dictionary<string, string> messageProperties)
    {
        return !string.IsNullOrEmpty(
            AttachmentUtility.GetAttachmentIds(messageProperties).FirstOrDefault()
        );
    }
}
