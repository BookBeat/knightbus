using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Deduplication;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class DeduplicationMiddlewareTests
{
    private Mock<IDeduplicationStore> _store;
    private DeduplicationMiddleware _sut;

    [SetUp]
    public void Setup()
    {
        _store = new Mock<IDeduplicationStore>();
        _sut = new DeduplicationMiddleware(_store.Object);
    }

    [Test]
    public async Task Should_call_next_and_not_release_for_non_deduplicatable_message()
    {
        var next = new Mock<IMessageProcessor>();
        var stateHandler = new Mock<IMessageStateHandler<PlainCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(new PlainCommand());
        stateHandler.Setup(x => x.MessageProperties).Returns(new Dictionary<string, string>());

        await _sut.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            next.Object,
            CancellationToken.None
        );

        next.Verify(x => x.ProcessAsync(stateHandler.Object, CancellationToken.None), Times.Once);
        _store.Verify(
            s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task Should_call_next_and_not_release_for_time_window_mode()
    {
        var next = new Mock<IMessageProcessor>();
        var message = new DeduplicatableCommand { Window = TimeSpan.FromMinutes(5) };
        var stateHandler = new Mock<IMessageStateHandler<DeduplicatableCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        stateHandler
            .Setup(x => x.MessageProperties)
            .Returns(
                new Dictionary<string, string>
                {
                    { DeduplicationPreProcessor.DeduplicationKeyProperty, "my-key" },
                }
            );

        await _sut.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            next.Object,
            CancellationToken.None
        );

        next.Verify(x => x.ProcessAsync(stateHandler.Object, CancellationToken.None), Times.Once);
        _store.Verify(
            s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task Should_release_key_after_processing_in_outbox_mode()
    {
        var next = new Mock<IMessageProcessor>();
        var message = new DeduplicatableCommand { Key = "my-key", Window = null };
        var stateHandler = new Mock<IMessageStateHandler<DeduplicatableCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        stateHandler
            .Setup(x => x.MessageProperties)
            .Returns(
                new Dictionary<string, string>
                {
                    { DeduplicationPreProcessor.DeduplicationKeyProperty, "my-key" },
                }
            );

        await _sut.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            next.Object,
            CancellationToken.None
        );

        next.Verify(x => x.ProcessAsync(stateHandler.Object, CancellationToken.None), Times.Once);
        _store.Verify(s => s.ReleaseAsync("my-key", CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task Should_not_release_when_dedup_key_property_is_missing()
    {
        var next = new Mock<IMessageProcessor>();
        var message = new DeduplicatableCommand { Key = "my-key", Window = null };
        var stateHandler = new Mock<IMessageStateHandler<DeduplicatableCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        stateHandler.Setup(x => x.MessageProperties).Returns(new Dictionary<string, string>());

        await _sut.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            next.Object,
            CancellationToken.None
        );

        _store.Verify(
            s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    public class PlainCommand : ICommand { }

    public class DeduplicatableCommand : ICommand, IDeduplicatable
    {
        public string Key { get; set; } = "default-key";
        public TimeSpan? Window { get; set; } = null;

        public string DeduplicationKey => Key;
        public TimeSpan? DeduplicationWindow => Window;
    }
}
