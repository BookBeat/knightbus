using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

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
        private readonly CloudQueue _queue;
        private readonly CloudQueue _dlQueue;
        private readonly CloudBlobContainer _container;

        public StorageQueueClient(IStorageBusConfiguration configuration, IMessageAttachmentProvider attachmentProvider, string queueName)
        {
            _attachmentProvider = attachmentProvider;
            _queueName = queueName;
            _serializer = configuration.MessageSerializer;
            var storage = CloudStorageAccount.Parse(configuration.ConnectionString);
            var queueClient = storage.CreateCloudQueueClient();
            var blobClient = storage.CreateCloudBlobClient();

            _queue = queueClient.GetQueueReference(queueName);
            _dlQueue = queueClient.GetQueueReference(GetDeadLetterName(queueName));
            _container = blobClient.GetContainerReference(queueName);
        }

        public static string GetDeadLetterName(string queueName)
        {
            return $"{queueName}-dl";
        }

        public async Task<int> GetQueueCountAsync()
        {
            await _queue.FetchAttributesAsync().ConfigureAwait(false);
            return _queue.ApproximateMessageCount ?? 0;
        }

        public async Task DeadLetterAsync(StorageQueueMessage message)
        {
            var deadLetterMessage = new CloudQueueMessage(_serializer.Serialize(message.Properties));
            await _dlQueue.AddMessageAsync(deadLetterMessage, TimeSpan.MaxValue, null, null, null)
                .ContinueWith(task => _queue.DeleteMessageAsync(message.QueueMessageId, message.PopReceipt), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ConfigureAwait(false);
        }

        public async Task<int> GetDeadLetterCountAsync()
        {
            await _dlQueue.FetchAttributesAsync().ConfigureAwait(false);
            return _dlQueue.ApproximateMessageCount ?? 0;
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
            var cloudQueueMessage = new CloudQueueMessage(message.QueueMessageId, message.PopReceipt);
            cloudQueueMessage.SetMessageContent(_serializer.Serialize(message.Properties));
            await _queue.UpdateMessageAsync(cloudQueueMessage, visibilityTimeout.Value, MessageUpdateFields.Content | MessageUpdateFields.Visibility).ConfigureAwait(false);
            message.PopReceipt = cloudQueueMessage.PopReceipt;
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

            //Create hidden queue message
            var cloudMessage = new CloudQueueMessage(_serializer.Serialize(storageMessage.Properties));
            try
            {
                var blob = _container.GetBlockBlobReference(storageMessage.BlobMessageId);
                await blob.UploadTextAsync(_serializer.Serialize(message)).ConfigureAwait(false);
                await _queue.AddMessageAsync(cloudMessage, TimeSpan.MaxValue, delay ?? TimeSpan.Zero, null, null).ConfigureAwait(false);
            }
            catch (Exception)
            {
                //If we cannot upload the blob or create the message try cleaning up and throw
                try
                {
                    await TryDeleteBlob(storageMessage.BlobMessageId).ConfigureAwait(false);
                    await _queue.DeleteMessageAsync(cloudMessage).ConfigureAwait(false);
                }
                catch (StorageException e) when (e.RequestInformation?.HttpStatusCode == 404)
                {
                    //Cannot erase what's not there
                }
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
                var cloudMessage = new CloudQueueMessage(_serializer.Serialize(deadletter.Properties));
                await _queue.AddMessageAsync(cloudMessage, TimeSpan.MaxValue, TimeSpan.Zero, null, null).ConfigureAwait(false);
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
                await _container.GetBlockBlobReference(id).DeleteAsync().ConfigureAwait(false);
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
            var cloudQueueMessage = new CloudQueueMessage(message.QueueMessageId, message.PopReceipt);
            await _queue.UpdateMessageAsync(cloudQueueMessage, timeToExtend, MessageUpdateFields.Visibility, null, null, cancellationToken).ConfigureAwait(false);
            message.PopReceipt = cloudQueueMessage.PopReceipt;
        }

        
        public Task<List<StorageQueueMessage>> GetMessagesAsync<T>(int count, TimeSpan? lockDuration) where T : IStorageQueueCommand
        {
            return GetMessagesAsync<T>(count, lockDuration, _queue);
        }
        private async Task<List<StorageQueueMessage>> GetMessagesAsync<T>(int count, TimeSpan? lockDuration, CloudQueue queue) where T : IStorageQueueCommand
        {
            var messages = (await queue.GetMessagesAsync(count, lockDuration, null, null).ConfigureAwait(false)).ToList();
            if (!messages.Any()) return new List<StorageQueueMessage>();
            var messageContainers = new List<StorageQueueMessage>(messages.Count);
            foreach (var queueMessage in messages)
            {
                try
                {
                    var message = new StorageQueueMessage
                    {
                        QueueMessageId = queueMessage.Id,
                        DequeueCount = queueMessage.DequeueCount,
                        PopReceipt = queueMessage.PopReceipt,
                        Properties = TryDeserializeProperties(queueMessage.AsString)
                    };
                    var content = await _container.GetBlockBlobReference(message.BlobMessageId).DownloadTextAsync().ConfigureAwait(false);
                    message.Message = _serializer.Deserialize<T>(content);
                    messageContainers.Add(message);
                }
                catch (StorageException e) when (e.RequestInformation?.HttpStatusCode == 404)
                {
                    var deadLetterMessage = new StorageQueueMessage(_serializer.Deserialize<T>("{}"))
                    {
                        QueueMessageId = queueMessage.Id,
                        DequeueCount = queueMessage.DequeueCount,
                        PopReceipt = queueMessage.PopReceipt,
                        Properties = TryDeserializeProperties(queueMessage.AsString)
                    };

                    deadLetterMessage.Properties["Error"] = "Could not find the message blob. Something is really wrong.";

                    //If we cannot find the message blob something is really wrong, dead letter the message right away
                    await DeadLetterAsync(deadLetterMessage).ConfigureAwait(false);
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