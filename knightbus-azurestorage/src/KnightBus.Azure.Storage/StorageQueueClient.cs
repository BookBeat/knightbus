using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    public interface IStorageQueueClient
    {
        Task<int> GetQueueCountAsync();
        Task DeadLetterAsync(StorageQueueMessage message);
        Task<int> GetDeadLetterCountAsync();
        Task<List<StorageQueueMessage>> PeekDeadLettersAsync<T>(int count) where T : IStorageQueueCommand;
        Task<StorageQueueMessage> ReceiveDeadLetterAsync<T>() where T : IStorageQueueCommand;
        Task RequeueDeadLettersAsync<T>(int count, Func<T, bool> shouldRequeue) where T : IStorageQueueCommand;
        Task CompleteAsync(StorageQueueMessage message);
        Task AbandonByErrorAsync(StorageQueueMessage message, TimeSpan? visibilityTimeout);
        Task SendAsync<T>(T message, TimeSpan? delay) where T : IStorageQueueCommand;
        Task<List<StorageQueueMessage>> GetMessagesAsync<T>(int count, TimeSpan? lockDuration) where T : IStorageQueueCommand;
        Task CreateIfNotExistsAsync();
        Task DeleteIfExistsAsync();
        Task SetVisibilityTimeout(StorageQueueMessage message, TimeSpan timeToExtend, CancellationToken cancellationToken);
    }

    public class StorageQueueClient : IStorageQueueClient
    {
        private readonly string _queueName;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageAttachmentProvider _attachmentProvider;
        private readonly QueueClient _queue;
        private readonly QueueClient _dlQueue;
        private readonly BlobContainerClient _container;

        public StorageQueueClient(IStorageBusConfiguration configuration, IMessageAttachmentProvider attachmentProvider, string queueName)
        {
            _attachmentProvider = attachmentProvider;
            _queueName = queueName;
            _serializer = configuration.MessageSerializer;

            //QueueMessageEncoding.Base64 required for backwards compability with v11 storage clients
            _queue = new QueueClient(configuration.ConnectionString, queueName, new QueueClientOptions {MessageEncoding = QueueMessageEncoding.Base64});
            _dlQueue = new QueueClient(configuration.ConnectionString, GetDeadLetterName(queueName), new QueueClientOptions {MessageEncoding = QueueMessageEncoding.Base64});
            _container = new BlobContainerClient(configuration.ConnectionString, queueName);
        }

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
            await _dlQueue.SendMessageAsync(_serializer.Serialize(message.Properties), timeToLive:TimeSpan.FromSeconds(-1))
                .ContinueWith(task => _queue.DeleteMessageAsync(message.QueueMessageId, message.PopReceipt), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ConfigureAwait(false);
        }

        public async Task<int> GetDeadLetterCountAsync()
        {
            var props = await _dlQueue.GetPropertiesAsync().ConfigureAwait(false);
            return props.Value.ApproximateMessagesCount;
        }

        public async Task CompleteAsync(StorageQueueMessage message)
        {
            await _queue.DeleteMessageAsync(message.QueueMessageId, message.PopReceipt)
                .ContinueWith(task=> TryDeleteBlob(message.BlobMessageId), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ConfigureAwait(false);
        }

        public async Task AbandonByErrorAsync(StorageQueueMessage message, TimeSpan? visibilityTimeout)
        {
            visibilityTimeout = visibilityTimeout ?? TimeSpan.Zero;
            var result = await _queue.UpdateMessageAsync(message.QueueMessageId, message.PopReceipt, _serializer.Serialize(message.Properties), visibilityTimeout.Value).ConfigureAwait(false);
            message.PopReceipt = result.Value.PopReceipt;
        }

        public async Task SendAsync<T>(T message, TimeSpan? delay) where T : IStorageQueueCommand
        {

            var storageMessage = new StorageQueueMessage(message)
            {
                BlobMessageId = Guid.NewGuid().ToString()
            };
            
            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                if(_attachmentProvider == null) throw new AttachmentProviderMissingException();

                var attachmentMessage = (ICommandWithAttachment)message;
                if (attachmentMessage.Attachment != null)
                {
                    var attachmentIds = new List<string>();
                    var attachmentId = Guid.NewGuid().ToString("N");
                    await _attachmentProvider.UploadAttachmentAsync(_queueName, attachmentId, attachmentMessage.Attachment).ConfigureAwait(false);
                    attachmentIds.Add(attachmentId);
                    storageMessage.Properties[AttachmentUtility.AttachmentKey] = string.Join(",", attachmentIds);
                }
            }

            try
            {
                var blob = _container.GetBlobClient(storageMessage.BlobMessageId);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(_serializer.Serialize(message))))
                {
                    await blob.UploadAsync(stream).ConfigureAwait(false);
                }
                await _queue.SendMessageAsync(_serializer.Serialize(storageMessage.Properties), delay ?? TimeSpan.Zero, TimeSpan.FromSeconds(-1)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                //If we cannot upload the blob or create the message try cleaning up and throw
                await TryDeleteBlob(storageMessage.BlobMessageId).ConfigureAwait(false);
                throw;
            }
        }

        public Task<List<StorageQueueMessage>> PeekDeadLettersAsync<T>(int count) where T : IStorageQueueCommand
        {
            return GetMessagesAsync<T>(count, null, _dlQueue);
        }

        public async Task RequeueDeadLettersAsync<T>(int count, Func<T, bool> shouldRequeue) where T : IStorageQueueCommand
        {
            for (var i = 0; i < count; i++)
            {
                var messages = await GetMessagesAsync<T>(1, TimeSpan.FromMinutes(2), _dlQueue)
                    .ConfigureAwait(false);
                
                if(!messages.Any())
                    continue;
                var deadletter = messages.Single();
                
                // Delete the old message
                await _dlQueue.DeleteMessageAsync(deadletter.QueueMessageId, deadletter.PopReceipt).ConfigureAwait(false);
                
                // The dead letter will be deleted even though shouldRequeue is false
                if(shouldRequeue != null && !shouldRequeue((T)deadletter.Message))
                    continue;
                
                // enqueue the new message again
                await _queue.SendMessageAsync(_serializer.Serialize(deadletter.Properties), TimeSpan.Zero, TimeSpan.FromSeconds(-1)).ConfigureAwait(false);
            }
        }

        public async Task<StorageQueueMessage> ReceiveDeadLetterAsync<T>() where T : IStorageQueueCommand
        {
            var messages = await PeekDeadLettersAsync<T>(1).ConfigureAwait(false);
            if (!messages.Any()) return null;
            var message = messages.Single();

            await _dlQueue.DeleteMessageAsync(message.QueueMessageId, message.PopReceipt)
                .ContinueWith(task => TryDeleteBlob(message.BlobMessageId), TaskContinuationOptions.OnlyOnRanToCompletion).ConfigureAwait(false);

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
            ).ConfigureAwait(false);
        }
        public async Task DeleteIfExistsAsync()
        {
            await Task.WhenAll(
                _container.DeleteIfExistsAsync(),
                _queue.DeleteIfExistsAsync(),
                _dlQueue.DeleteIfExistsAsync()
            ).ConfigureAwait(false);
        }

        public async Task SetVisibilityTimeout(StorageQueueMessage message, TimeSpan timeToExtend, CancellationToken cancellationToken)
        {
            var result = await _queue.UpdateMessageAsync(message.QueueMessageId, message.PopReceipt, visibilityTimeout:timeToExtend, cancellationToken:cancellationToken).ConfigureAwait(false);
            message.PopReceipt = result.Value.PopReceipt;
        }

        
        public Task<List<StorageQueueMessage>> GetMessagesAsync<T>(int count, TimeSpan? lockDuration) where T : IStorageQueueCommand
        {
            return GetMessagesAsync<T>(count, lockDuration, _queue);
        }
        private async Task<List<StorageQueueMessage>> GetMessagesAsync<T>(int count, TimeSpan? lockDuration, QueueClient queue) where T : IStorageQueueCommand
        {
            var messages = (await queue.ReceiveMessagesAsync(count, lockDuration).ConfigureAwait(false)).Value;
            if (!messages.Any()) return new List<StorageQueueMessage>();
            var messageContainers = new List<StorageQueueMessage>(messages.Length);
            foreach (var queueMessage in messages)
            {
                try
                {
                    var message = new StorageQueueMessage
                    {
                        QueueMessageId = queueMessage.MessageId,
                        DequeueCount = (int) queueMessage.DequeueCount,
                        PopReceipt = queueMessage.PopReceipt,
                        Properties = TryDeserializeProperties(queueMessage.MessageText)
                    };
                    using (var steam = new MemoryStream())
                    {
                        await _container.GetBlobClient(message.BlobMessageId).DownloadToAsync(steam).ConfigureAwait(false);
                        steam.Position = 0;
                        using (var streamReader = new StreamReader(steam, Encoding.UTF8))
                        {
                            message.Message = _serializer.Deserialize<T>(await streamReader.ReadToEndAsync().ConfigureAwait(false));
                            messageContainers.Add(message);
                        }
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

        private Dictionary<string, string> TryDeserializeProperties(string serialized)
        {
            try
            {
                return _serializer.Deserialize<Dictionary<string, string>>(serialized);
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
    }
}