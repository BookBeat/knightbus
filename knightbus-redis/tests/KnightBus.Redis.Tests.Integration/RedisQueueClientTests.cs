using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using NUnit.Framework;

namespace KnightBus.Redis.Tests.Integration
{
    public class RedisQueueClientTests
    {
        private IRedisBus _bus;
        private RedisQueueClient<TestCommand> _target;
        private readonly string _queueName = AutoMessageMapper.GetQueueName<TestCommand>();

        [SetUp]
        public void Setup()
        {
            _bus = new RedisBus(RedisTestBase.Configuration);
            _target = new RedisQueueClient<TestCommand>(RedisTestBase.Database);
        }
        [TearDown] //This should be done after each test thus not OneTime
        public void BaseTeardown()
        {
            var server = RedisTestBase.Database.Multiplexer.GetServer(RedisTestBase.Configuration.ConnectionString);
            server.FlushDatabase();
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

            //Verify processing queue empty
            var processingQueueLength = await RedisTestBase.Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
            processingQueueLength.Should().Be(0);

            //Act
            var messages = await _target.GetMessagesAsync(5);

            //Assert
            messages.Length.Should().Be(1);
            var message = messages.First();
            message.Message.Should().BeEquivalentTo(command);
            message.ExpirationKey.Should().NotBeNullOrEmpty();
            message.HashEntries.Should().ContainKey(RedisHashKeys.DeliveryCount);
            var deliveryCount = message.HashEntries.First(e => e.Key.Equals(RedisHashKeys.DeliveryCount));
            deliveryCount.Value.Should().Be("1");
            processingQueueLength = await RedisTestBase.Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
            processingQueueLength.Should().Be(1);
        }

        [Test]
        public async Task CompleteMessageAsync_should_delete_hash_and_expiration_keys_then_remove_message()
        {
            //Arrange
            var message = await SendAndGetMessage();

            //Act
            await _target.CompleteMessageAsync(message);

            //Assert
            var hash = await RedisTestBase.Database.HashGetAllAsync(message.HashKey);
            hash.Should().BeEmpty();
            var expirationKey = await RedisTestBase.Database.StringGetAsync(message.ExpirationKey);
            expirationKey.HasValue.Should().BeFalse();
            var messages = await _target.GetMessagesAsync(1);
            messages.Length.Should().Be(0);
            var processingQueueLength = await RedisTestBase.Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
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
            var hash = await RedisTestBase.Database.HashGetAllAsync(message.HashKey);
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

            //Act
            await _target.DeadletterMessageAsync(message, 1);

            //Assert
            var processingQueueLength = await RedisTestBase.Database.ListLengthAsync(RedisQueueConventions.GetProcessingQueueName(_queueName));
            processingQueueLength.Should().Be(0);
            var deadLetterQueueLength = await RedisTestBase.Database.ListLengthAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName));
            deadLetterQueueLength.Should().Be(1);
        }

        [Test]
        public async Task RequeueDeadletterAsync_should_requeue_message_remove_errors_and_delivery_count()
        {
            //Arrange
            var message = await SendAndGetMessage();
            await _target.DeadletterMessageAsync(message, 1);

            //Act
            await _target.RequeueDeadletterAsync();

            //Assert
            var deadLetterQueueLength = await RedisTestBase.Database.ListLengthAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName));
            deadLetterQueueLength.Should().Be(0);
            var queueLength = await RedisTestBase.Database.ListLengthAsync(_queueName);
            queueLength.Should().Be(1);
        }

        [Test]
        public async Task GetDeadlettersAsync_should_return_deadletters()
        {
            //Arrange
            var message = await SendAndGetMessage();
            await _target.DeadletterMessageAsync(message, 1);

            //Act
            var deadletters = await _target.PeekDeadlettersAsync(1).ToListAsync();

            //Assert
            deadletters.Should().ContainSingle();
            var deadletter = deadletters.First();
            deadletter.Message.Body.Value.Should().Be(message.Message.Value);
            deadletter.HashEntries.Should().NotBeEmpty();
        }

        [Test]
        public async Task DeleteDeadletter_should_delete_deadletter_and_related_data()
        {
            //Arrange
            var message = await SendAndGetMessage();
            await _target.DeadletterMessageAsync(message, 1);

            var deadletter = await _target.PeekDeadlettersAsync(1).FirstAsync();

            //Act
            await _target.DeleteDeadletterAsync(deadletter);

            //Assert
            var deadletters = await _target.PeekDeadlettersAsync(1).ToListAsync();
            deadletters.Should().BeEmpty();

            var hashKey = RedisQueueConventions.GetMessageHashKey(_queueName, deadletter.Message.Id);
            var hashes = await RedisTestBase.Database.HashGetAllAsync(hashKey).ConfigureAwait(false);
            hashes.Should().BeEmpty();

            var expirationKey = RedisQueueConventions.GetMessageExpirationKey(_queueName, deadletter.Message.Id);
            var expiration = await RedisTestBase.Database.StringGetAsync(expirationKey);
            expiration.IsNull.Should().BeTrue();
        }
    }
}
