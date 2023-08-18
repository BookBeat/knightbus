using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using KnightBus.Nats.Messages;
using NATS.Client;
using NATS.Client.JetStream;

namespace KnightBus.Nats
{
    public interface IJetStreamBus
    {
        Task Send(IJetStreamCommand message, CancellationToken cancellationToken = default);
        Task Publish(IJetStreamEvent message, CancellationToken cancellationToken = default);
        Task<TResponse> RequestAsync<T, TResponse>(IJetStreamRequest request, CancellationToken cancellationToken = default) where T : IJetStreamRequest;
        IEnumerable<TResponse> RequestStream<T, TResponse>(IJetStreamRequest command, CancellationToken cancellationToken = default) where T : IJetStreamRequest;
    }

    public class JetStreamBus : IJetStreamBus
    {
        private readonly IConnection _connection;
        private readonly IMessageAttachmentProvider _attachmentProvider;
        private readonly IJetStreamConfiguration _configuration;
        private readonly IJetStream _streamContext;


        public JetStreamBus(IConnection connection, IJetStreamConfiguration configuration,
            IMessageAttachmentProvider attachmentProvider = null)
        {
            _connection = connection;
            _streamContext = _connection.CreateJetStreamContext(configuration.JetStreamOptions);
            _configuration = configuration;
            _attachmentProvider = attachmentProvider;
        }

        public Task Send(IJetStreamCommand message, CancellationToken cancellationToken = default)
        {
            return SendInternal(message, cancellationToken);
        }

        public Task Publish(IJetStreamEvent message, CancellationToken cancellationToken = default)
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
                    await _attachmentProvider
                        .UploadAttachmentAsync(mapping.QueueName, id, attachmentMessage.Attachment, cancellationToken)
                        .ConfigureAwait(false);
                    attachmentIds.Add(id);
                    msg.Header.Add(AttachmentUtility.AttachmentKey, string.Join(",", attachmentIds));
                }
            }

            await _streamContext.PublishAsync(msg).ConfigureAwait(false);
        }

        public async Task<TResponse> RequestAsync<T, TResponse>(IJetStreamRequest request,
            CancellationToken cancellationToken = default) where T : IJetStreamRequest
        {
            var mapping = AutoMessageMapper.GetMapping(request.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var reply = await _connection
                .RequestAsync(mapping.QueueName, serializer.Serialize(request), cancellationToken)
                .ConfigureAwait(false);
            ThrowIfErrorResponse(reply);
            return serializer.Deserialize<TResponse>(reply.Data.AsSpan());
        }

        public IEnumerable<TResponse> RequestStream<T, TResponse>(IJetStreamRequest command,
            CancellationToken cancellationToken = default) where T : IJetStreamRequest
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
                msg.Ack();
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
