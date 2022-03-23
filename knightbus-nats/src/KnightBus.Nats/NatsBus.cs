using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using KnightBus.Nats.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    public interface INatsBus
    {
        Task Send(INatsCommand message, CancellationToken cancellationToken = default);
        Task Publish(INatsEvent message, CancellationToken cancellationToken = default);
        Task<TResponse> RequestAsync<T, TResponse>(INatsRequest request, CancellationToken cancellationToken = default) where T : INatsRequest;
        IEnumerable<TResponse> RequestStream<T, TResponse>(INatsRequest command, CancellationToken cancellationToken = default) where T : INatsRequest;
    }

    public class NatsBus : INatsBus
    {
        private readonly IConnection _connection;
        private readonly INatsBusConfiguration _configuration;
        private IMessageAttachmentProvider _attachmentProvider;

        public void EnableAttachments(IMessageAttachmentProvider attachmentProvider)
        {
            _attachmentProvider = attachmentProvider;
        }

        public NatsBus(IConnection connection, INatsBusConfiguration configuration)
        {
            _connection = connection;
            _configuration = configuration;
        }

        public  Task Send(INatsCommand message, CancellationToken cancellationToken = default)
        {
            return SendInternal(message, cancellationToken);
        }

        public Task Publish(INatsEvent message, CancellationToken cancellationToken = default)
        {
            return SendInternal(message, cancellationToken);
        }

        private async Task SendInternal(IMessage message, CancellationToken cancellationToken = default)
        {
            var mapping = AutoMessageMapper.GetMapping(message.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var msg = new Msg(mapping.QueueName, serializer.Serialize(message));

            if (message is ICommandWithAttachment attachmentMessage)
            {
                if (_attachmentProvider == null) throw new AttachmentProviderMissingException();

                if (attachmentMessage.Attachment != null)
                {
                    var attachmentIds = new List<string>();
                    var id = Guid.NewGuid().ToString("N");
                    await _attachmentProvider.UploadAttachmentAsync(mapping.QueueName, id, attachmentMessage.Attachment, cancellationToken).ConfigureAwait(false);
                    attachmentIds.Add(id);
                    msg.Header.Add(AttachmentUtility.AttachmentKey, string.Join(",", attachmentIds));
                }
            }
            _connection.Publish(msg);
        }

        public async Task<TResponse> RequestAsync<T, TResponse>(INatsRequest request, CancellationToken cancellationToken = default) where T : INatsRequest
        {
            var mapping = AutoMessageMapper.GetMapping(request.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var reply = await _connection.RequestAsync(mapping.QueueName, serializer.Serialize(request), cancellationToken).ConfigureAwait(false);
            ThrowIfErrorResponse(reply);
            return serializer.Deserialize<TResponse>(reply.Data.AsSpan());
        }

        public IEnumerable<TResponse> RequestStream<T, TResponse>(INatsRequest command, CancellationToken cancellationToken = default) where T : INatsRequest
        {
            var mapping = AutoMessageMapper.GetMapping(command.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var inbox = Guid.NewGuid().ToString("N");
            using var sub = _connection.SubscribeSync(inbox);
            _connection.Publish(mapping.QueueName, inbox, serializer.Serialize(command));

            do
            {
                var msg = sub.NextMessage();
                if (msg.Data.Length == 0)
                {
                    if (msg.HasHeaders && msg.Header[MsgConstants.HeaderName] == MsgConstants.Completed)
                    {
                        break;
                    }
                    ThrowIfErrorResponse(msg);

                    yield return default;
                }

                yield return serializer.Deserialize<TResponse>(msg.Data.AsSpan());

            } while (true);
            sub.Unsubscribe();
        }

        private void ThrowIfErrorResponse(Msg msg)
        {
            if (msg.Data.Length == 0 && msg.HasHeaders && msg.Header[MsgConstants.HeaderName] == MsgConstants.Error)
            {
                throw new NATSException("Receiver failed");
            }
        }
    }
}