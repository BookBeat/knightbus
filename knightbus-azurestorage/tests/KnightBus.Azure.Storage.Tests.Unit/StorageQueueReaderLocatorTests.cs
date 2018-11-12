using FluentAssertions;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit
{
    [TestFixture]
    public class StorageQueueReaderLocatorTests
    {
        [Test]
        public void Should_allow_creation_when_message_is_storage_message()
        {
            //arrange
            var factory = new StorageQueueTransportFactory(new StorageBusConfiguration(""));
            var message = new Mock<IStorageQueueCommand>();
            //act
            var canCreate = factory.CanCreate(message.Object.GetType());
            //assert
            canCreate.Should().BeTrue();
        }

        [Test]
        public void Should_disallow_creation_when_message_is_not_storage_message()
        {
            //arrange
            var factory = new StorageQueueTransportFactory(new StorageBusConfiguration(""));
            var message = new Mock<ICommand>();
            //act
            var canCreate = factory.CanCreate(message.Object.GetType());
            //assert
            canCreate.Should().BeFalse();
        }

        [Test]
        public void Should_create_instance()
        {
            //arrange
            var factory = new StorageQueueTransportFactory(new StorageBusConfiguration(""));
            var message = new Mock<IStorageQueueCommand>();
            //act
            var result = factory.Create(message.Object.GetType(), null, typeof(TestMessageSettings), Mock.Of<IHostConfiguration>(), Mock.Of<IMessageProcessor>());
            //assert
            result.Should().NotBeNull();
        }
    }
}
