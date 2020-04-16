using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using NUnit.Framework;

namespace KnightBus.Redis.Tests.Integration
{
    public class RedisQueueClientTests : RedisTestBase
    {
        private IRedisBus _bus;
        private RedisQueueClient<TestCommand> _target;
        private readonly string _queueName = AutoMessageMapper.GetQueueName<TestCommand>();

        [SetUp]
        public void Setup()
        {
            _bus = new RedisBus(Configuration);
            _target = new RedisQueueClient<TestCommand>(Database);
        }

        private async Task<RedisMessage<TestCommand>> SendAndGetMessage()
        {
            var command = new TestCommand(Guid.NewGuid().ToString());
            await _bus.SendAsync(command);
            var messages = await _target.GetMessagesAsync(1);
            return messages.First();
        }

        [Test]
        public async Task GetMessagesAsync_should_get_messages()
        {
            //Arrange
            var command = new TestCommand(Guid.NewGuid().ToString());
            await _bus.SendAsync(command);

            //Act
            var messages = await _target.GetMessagesAsync(5);

            //Assert
            messages.Length.Should().Be(1);
            var message = messages.First();
            message.Message.Should().BeEquivalentTo(command);
        }

        [Test]
        public async Task CompleteMessageAsync_should_delete_hash_and_expiration_keys_then_remove_message()
        {
            //Arrange
            var message = await SendAndGetMessage();
            var hash = await Database.HashGetAllAsync(message.HashKey);
            hash.Should().NotBeEmpty();
            var expirationKey = await Database.StringGetAsync(message.ExpirationKey);
            expirationKey.Should().NotBeNull();
            var processingQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
            processingQueueLength.Should().Be(1);

            //Act
            await _target.CompleteMessageAsync(message);

            //Assert
            hash = await Database.HashGetAllAsync(message.HashKey);
            hash.Should().BeEmpty();
            expirationKey = await Database.StringGetAsync(message.ExpirationKey);
            expirationKey.HasValue.Should().BeFalse();
            var messages = await _target.GetMessagesAsync(1);
            messages.Length.Should().Be(0);
            processingQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
            processingQueueLength.Should().Be(0);
        }

        [Test]
        public async Task AbandonMessageByErrorAsync_should_requeue_message_and_set_error_hash()
        {
            //Arrange
            var message = await SendAndGetMessage();
            var exception = new Exception("Test exception");

            //Act
            await _target.AbandonMessageByErrorAsync(message, exception);

            //Assert
            var hash = await Database.HashGetAllAsync(message.HashKey);
            var errors = hash.First(k => k.Name.Equals(RedisHashKeys.Errors));
            var errorMessage = errors.Value.ToString().TrimEnd('\n');
            errorMessage.Should().Be(exception.Message);

            var reQueuedMessages = await _target.GetMessagesAsync(1);
            var reQueuedMessage = reQueuedMessages.First();
            reQueuedMessage.Message.Should().BeEquivalentTo(message.Message);
            reQueuedMessage.HashKey.Should().BeEquivalentTo(message.HashKey);
            reQueuedMessage.ExpirationKey.Should().BeEquivalentTo(message.ExpirationKey);
        }

        [Test]
        public async Task DeadLetterMessageAsync_should_remove_from_processing_queue_and_put_message_in_deadletter_queue()
        {
            //Arrange
            var message = await SendAndGetMessage();
            var processingQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
            processingQueueLength.Should().Be(1);
            var deadLetterQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName));
            deadLetterQueueLength.Should().Be(0);

            //Act
            await _target.DeadLetterMessageAsync(message, 1);

            //Assert
            processingQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
            processingQueueLength.Should().Be(0);
            deadLetterQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName));
            deadLetterQueueLength.Should().Be(1);
        }

        [Test]
        public async Task RequeueDeadletterAsync_should_requeue_message_remove_errors_and_delivery_count()
        {
            //Arrange
            var message = await SendAndGetMessage();
            await _target.DeadLetterMessageAsync(message, 1);
            var deadLetterQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName));
            deadLetterQueueLength.Should().Be(1);
            var hash = await Database.HashGetAllAsync(message.HashKey);
            var deliveryCount = hash.First(h => h.Name.Equals(RedisHashKeys.DeliveryCount));
            deliveryCount.Value.Should().Be(1);

            //Act
            await _target.RequeueDeadletterAsync();

            //Assert
            deadLetterQueueLength = await Database.ListLengthAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName));
            deadLetterQueueLength.Should().Be(0);
            hash = await Database.HashGetAllAsync(message.HashKey);
            hash.Should().NotContain(h => h.Name.Equals(RedisHashKeys.DeliveryCount));
        }
    }
}
