using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Deduplication;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class DeduplicationPreProcessorTests
{
    private Mock<IDeduplicationStore> _store;
    private DeduplicationPreProcessor _sut;

    [SetUp]
    public void Setup()
    {
        _store = new Mock<IDeduplicationStore>();
        _sut = new DeduplicationPreProcessor(_store.Object);
    }

    [Test]
    public async Task Should_return_Continue_for_non_deduplicatable_messages()
    {
        var result = await _sut.PreProcess(new PlainCommand(), CancellationToken.None);

        result.ShouldAbort.Should().BeFalse();
        result.Properties.Should().BeEmpty();
        _store.Verify(
            s =>
                s.TryClaimAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task Should_return_Abort_when_key_is_already_claimed()
    {
        _store
            .Setup(s =>
                s.TryClaimAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);

        var result = await _sut.PreProcess(new DeduplicatableCommand(), CancellationToken.None);

        result.ShouldAbort.Should().BeTrue();
    }

    [Test]
    public async Task Should_return_properties_with_dedup_key_when_key_is_claimed()
    {
        _store
            .Setup(s =>
                s.TryClaimAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        var command = new DeduplicatableCommand { Key = "book-index:42" };

        var result = await _sut.PreProcess(command, CancellationToken.None);

        result.ShouldAbort.Should().BeFalse();
        result.Properties.Should().ContainKey(DeduplicationPreProcessor.DeduplicationKeyProperty);
        result
            .Properties[DeduplicationPreProcessor.DeduplicationKeyProperty]
            .Should()
            .Be("book-index:42");
    }

    [Test]
    public async Task Should_pass_null_ttl_to_store_for_outbox_mode()
    {
        _store
            .Setup(s => s.TryClaimAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var command = new DeduplicatableCommand { Key = "my-key", Window = null };

        await _sut.PreProcess(command, CancellationToken.None);

        _store.Verify(s => s.TryClaimAsync("my-key", null, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task Should_pass_ttl_to_store_for_time_window_mode()
    {
        var window = TimeSpan.FromMinutes(5);
        _store
            .Setup(s => s.TryClaimAsync(It.IsAny<string>(), window, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var command = new DeduplicatableCommand { Key = "my-key", Window = window };

        await _sut.PreProcess(command, CancellationToken.None);

        _store.Verify(s => s.TryClaimAsync("my-key", window, CancellationToken.None), Times.Once);
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
