using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Azure;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.Storage;

internal class StorageQueueMessagePump : GenericMessagePump<StorageQueueMessage, IStorageQueueCommand>
{
    private readonly IStorageQueueClient _storageQueueClient;

    public StorageQueueMessagePump(IStorageQueueClient storageQueueClient, IProcessingSettings settings, ILogger log) : base(settings, log)
    {
        _storageQueueClient = storageQueueClient;
    }


    protected override async IAsyncEnumerable<StorageQueueMessage> GetMessagesAsync<TMessage>(int count, TimeSpan? lockDuration)
    {
        var messages = await _storageQueueClient.GetMessagesAsync<TMessage>(count, lockDuration);
        foreach (var message in messages)
        {
            yield return message;
        }
    }

    protected override async Task CreateChannel(Type messageType)
    {
        await _storageQueueClient.CreateIfNotExistsAsync().ConfigureAwait(false);
    }

    protected override bool ShouldCreateChannel(Exception e)
    {
        return e is RequestFailedException { Status: (int)HttpStatusCode.NotFound };
    }

    protected override Task CleanupResources()
    {
        return Task.CompletedTask;
    }

    protected override TimeSpan PollingDelay => TimeSpan.FromSeconds(5);
}
