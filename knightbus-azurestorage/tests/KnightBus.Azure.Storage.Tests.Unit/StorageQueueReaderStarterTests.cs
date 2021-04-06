using System;
using FluentAssertions;
using KnightBus.Core;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit
{
  [TestFixture]
  public class StorageQueueReaderStarterTests
  {
    [Test]
    public void Should_not_start_storage_queue_reader_with_too_high_prefetch_count()
    {
      //act
      Action act = () =>
      {
        var storageQueueReader = new StorageQueueChannelReceiver<LongRunningTestCommand>(
                  new TooHighPrefetchSettings(), Mock.Of<IMessageSerializer>(),
              Mock.Of<IMessageProcessor>(), Mock.Of<IHostConfiguration>(), new StorageBusConfiguration(""));
      };

      //assert
      act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private class TooHighPrefetchSettings : IProcessingSettings
    {
      public int MaxConcurrentCalls => 50;
      public int PrefetchCount => 33;
      public TimeSpan MessageLockTimeout => TimeSpan.MaxValue;
      public int DeadLetterDeliveryLimit => 1;
    }
  }
}
