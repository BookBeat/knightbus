using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage;

public interface IStorageQueueClient
{
    Task<int> GetQueueCountAsync();
    Task DeadLetterAsync(StorageQueueMessage message);
    Task<int> GetDeadLetterCountAsync();
    Task<List<StorageQueueMessage>> PeekDeadLettersAsync<T>(int count)
        where T : IStorageQueueCommand;
    Task<StorageQueueMessage> ReceiveDeadLetterAsync<T>()
        where T : IStorageQueueCommand;
    Task RequeueDeadLettersAsync<T>(int count, Func<T, bool> shouldRequeue)
        where T : IStorageQueueCommand;
    Task CompleteAsync(StorageQueueMessage message);
    Task AbandonByErrorAsync(StorageQueueMessage message, TimeSpan? visibilityTimeout);
    Task SendAsync<T>(T message, TimeSpan? delay, CancellationToken cancellationToken)
        where T : IStorageQueueCommand;
    Task<List<StorageQueueMessage>> PeekMessagesAsync<T>(int count)
        where T : IStorageQueueCommand;
    Task<List<StorageQueueMessage>> GetMessagesAsync<T>(int count, TimeSpan? lockDuration)
        where T : IStorageQueueCommand;
    Task CreateIfNotExistsAsync();
    Task DeleteIfExistsAsync();
    Task SetVisibilityTimeout(
        StorageQueueMessage message,
        TimeSpan timeToExtend,
        CancellationToken cancellationToken
    );
}

public class StorageQueueClient(
    IStorageBusConfiguration configuration,
    IMessageSerializer serializer,
    IEnumerable<IMessagePreProcessor> messagePreProcessors,
    string queueName
) : IStorageQueueClient
{
    //QueueMessageEncoding.Base64 required for backwards compability with v11 storage clients
    private readonly QueueClient _queue = new(
        configuration.ConnectionString,
        queueName,
        new QueueClientOptions { MessageEncoding = configuration.MessageEncoding }
    );
    private readonly QueueClient _dlQueue = new(
        configuration.ConnectionString,
        GetDeadLetterName(queueName),
        new QueueClientOptions { MessageEncoding = configuration.MessageEncoding }
    );
    private readonly BlobContainerClient _container = new(
        configuration.ConnectionString,
        queueName
    );

    public static string GetDeadLetterName(string queueName)
    {
        return $"{queueName}-dl";
    }

    public async Task<int> GetQueueCountAsync()
    {
        var props = await _queue.GetPropertiesAsync().ConfigureAwait(false);
        return props.Value.ApproximateMessagesCount;
    }

    public async Task DeadLetterAsync(StorageQueueMessage message)
    {
        await _dlQueue
            .SendMessageAsync(
                new BinaryData(serializer.Serialize(message.Properties)),
                timeToLive: TimeSpan.FromSeconds(-1)
            )
            .ConfigureAwait(false);
        await _queue
            .DeleteMessageAsync(message.QueueMessageId, message.PopReceipt)
            .ConfigureAwait(false);
    }

    public async Task<int> GetDeadLetterCountAsync()
    {
        var props = await _dlQueue.GetPropertiesAsync().ConfigureAwait(false);
        return props.Value.ApproximateMessagesCount;
    }

    public async Task CompleteAsync(StorageQueueMessage message)
    {
        await _queue
            .DeleteMessageAsync(message.QueueMessageId, message.PopReceipt)
            .ConfigureAwait(false);
        await TryDeleteBlob(message.BlobMessageId).ConfigureAwait(false);
    }

    public async Task AbandonByErrorAsync(StorageQueueMessage message, TimeSpan? visibilityTimeout)
    {
        visibilityTimeout = visibilityTimeout ?? TimeSpan.Zero;
        var result = await _queue
            .UpdateMessageAsync(
                message.QueueMessageId,
                message.PopReceipt,
                new BinaryData(serializer.Serialize(message.Properties)),
                visibilityTimeout.Value
            )
            .ConfigureAwait(false);
        message.PopReceipt = result.Value.PopReceipt;
    }

    public async Task SendAsync<T>(
        T message,
        TimeSpan? delay,
        CancellationToken cancellationToken = default
    )
        where T : IStorageQueueCommand
    {
        var storageMessage = new StorageQueueMessage(message)
        {
            BlobMessageId = Guid.NewGuid().ToString(),
        };

        foreach (var preProcessor in messagePreProcessors)
        {
            var properties = await preProcessor.PreProcess(message, cancellationToken);
            foreach (var property in properties)
            {
                storageMessage.Properties[property.Key] = property.Value.ToString();
            }
        }

        try
        {
            var blob = _container.GetBlobClient(storageMessage.BlobMessageId);
            using (var stream = new MemoryStream(serializer.Serialize(message)))
            {
                await blob.UploadAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            await _queue
                .SendMessageAsync(
                    new BinaryData(serializer.Serialize(storageMessage.Properties)),
                    delay ?? TimeSpan.Zero,
                    TimeSpan.FromSeconds(-1),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            //If we cannot upload the blob or create the message try cleaning up and throw
            await TryDeleteBlob(storageMessage.BlobMessageId).ConfigureAwait(false);
            throw;
        }
    }

    public Task<List<StorageQueueMessage>> PeekDeadLettersAsync<T>(int count)
        where T : IStorageQueueCommand
    {
        return PeekMessagesAsync<T>(count, _dlQueue);
    }

    public async Task RequeueDeadLettersAsync<T>(int count, Func<T, bool> shouldRequeue)
        where T : IStorageQueueCommand
    {
        for (var i = 0; i < count; i++)
        {
            var messages = await GetMessagesAsync<T>(1, TimeSpan.FromMinutes(2), _dlQueue)
                .ConfigureAwait(false);

            if (!messages.Any())
                continue;
            var deadletter = messages.Single();

            // Delete the old message
            await _dlQueue
                .DeleteMessageAsync(deadletter.QueueMessageId, deadletter.PopReceipt)
                .ConfigureAwait(false);

            // The dead letter will be deleted even though shouldRequeue is false
            if (shouldRequeue != null && !shouldRequeue((T)deadletter.Message))
                continue;

            // enqueue the new message again
            await _queue
                .SendMessageAsync(
                    new BinaryData(serializer.Serialize(deadletter.Properties)),
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(-1)
                )
                .ConfigureAwait(false);
        }
    }

    public async Task<StorageQueueMessage> ReceiveDeadLetterAsync<T>()
        where T : IStorageQueueCommand
    {
        var messages = await GetMessagesAsync<T>(1, null, _dlQueue).ConfigureAwait(false);
        if (!messages.Any())
            return null;
        var message = messages.Single();

        await _dlQueue
            .DeleteMessageAsync(message.QueueMessageId, message.PopReceipt)
            .ConfigureAwait(false);
        await TryDeleteBlob(message.BlobMessageId).ConfigureAwait(false);
        return message;
    }

    private async Task TryDeleteBlob(string id)
    {
        try
        {
            await _container.GetBlobClient(id).DeleteAsync().ConfigureAwait(false);
        }
        catch
        {
            //it's not critical the blob is deleted
        }
    }

    public async Task CreateIfNotExistsAsync()
    {
        await Task.WhenAll(
                _container.CreateIfNotExistsAsync(),
                _queue.CreateIfNotExistsAsync(),
                _dlQueue.CreateIfNotExistsAsync()
            )
            .ConfigureAwait(false);
    }

    public async Task DeleteIfExistsAsync()
    {
        await Task.WhenAll(
                _container.DeleteIfExistsAsync(),
                _queue.DeleteIfExistsAsync(),
                _dlQueue.DeleteIfExistsAsync()
            )
            .ConfigureAwait(false);
    }

    public async Task SetVisibilityTimeout(
        StorageQueueMessage message,
        TimeSpan timeToExtend,
        CancellationToken cancellationToken
    )
    {
        var result = await _queue
            .UpdateMessageAsync(
                message.QueueMessageId,
                message.PopReceipt,
                visibilityTimeout: timeToExtend,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        message.PopReceipt = result.Value.PopReceipt;
    }

    public Task<List<StorageQueueMessage>> GetMessagesAsync<T>(int count, TimeSpan? lockDuration)
        where T : IStorageQueueCommand
    {
        return GetMessagesAsync<T>(count, lockDuration, _queue);
    }

    private async Task<List<StorageQueueMessage>> GetMessagesAsync<T>(
        int count,
        TimeSpan? lockDuration,
        QueueClient queue
    )
        where T : IStorageQueueCommand
    {
        var messages = (
            await queue.ReceiveMessagesAsync(count, lockDuration).ConfigureAwait(false)
        ).Value;
        if (!messages.Any())
            return new List<StorageQueueMessage>();
        var messageContainers = new List<StorageQueueMessage>(messages.Length);
        foreach (var queueMessage in messages)
        {
            try
            {
                var message = new StorageQueueMessage
                {
                    QueueMessageId = queueMessage.MessageId,
                    DequeueCount = (int)queueMessage.DequeueCount,
                    PopReceipt = queueMessage.PopReceipt,
                    Time = queueMessage.InsertedOn,
                    Properties = TryDeserializeProperties(queueMessage.Body),
                };
                using (var stream = new MemoryStream())
                {
                    await _container
                        .GetBlobClient(message.BlobMessageId)
                        .DownloadToAsync(stream)
                        .ConfigureAwait(false);
                    stream.Position = 0;

                    message.Message = await serializer.Deserialize<T>(stream).ConfigureAwait(false);
                    messageContainers.Add(message);
                }
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                //If we cannot find the message blob something is really wrong, delete the message right away
                await queue.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
            }
        }

        return messageContainers;
    }

    public Task<List<StorageQueueMessage>> PeekMessagesAsync<T>(int count)
        where T : IStorageQueueCommand
    {
        return PeekMessagesAsync<T>(count, _queue);
    }

    private async Task<List<StorageQueueMessage>> PeekMessagesAsync<T>(int count, QueueClient queue)
        where T : IStorageQueueCommand
    {
        var messages = (await queue.PeekMessagesAsync(count).ConfigureAwait(false)).Value;
        if (!messages.Any())
            return new List<StorageQueueMessage>();
        var messageContainers = new List<StorageQueueMessage>(messages.Length);
        foreach (var queueMessage in messages)
        {
            try
            {
                var message = new StorageQueueMessage
                {
                    QueueMessageId = queueMessage.MessageId,
                    DequeueCount = (int)queueMessage.DequeueCount,
                    Properties = TryDeserializeProperties(queueMessage.Body),
                    Time = queueMessage.InsertedOn,
                };
                using (var stream = new MemoryStream())
                {
                    await _container
                        .GetBlobClient(message.BlobMessageId)
                        .DownloadToAsync(stream)
                        .ConfigureAwait(false);
                    stream.Position = 0;

                    message.Message = await serializer.Deserialize<T>(stream).ConfigureAwait(false);
                    messageContainers.Add(message);
                }
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                //Cannot modify a peeked message, ignore it
            }
        }

        return messageContainers;
    }

    private Dictionary<string, string> TryDeserializeProperties(BinaryData serialized)
    {
        try
        {
            return serializer.Deserialize<Dictionary<string, string>>(serialized.ToMemory());
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
