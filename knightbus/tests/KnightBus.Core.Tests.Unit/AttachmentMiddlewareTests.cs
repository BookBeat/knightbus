using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class AttachmentMiddlewareTests
{
    [Test]
    public async Task Should_attach_attachment_when_command_have_attachment()
    {
        //arrange
        var message = new AttachmentCommand();
        var nextProcessor = new Mock<IMessageProcessor>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is a stream"));
        var attachment = new MessageAttachment("test.txt", "text/plain", stream);
        var stateHandler = new Mock<IMessageStateHandler<AttachmentCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        stateHandler
            .Setup(x => x.MessageProperties)
            .Returns(
                new Dictionary<string, string>
                {
                    { AttachmentUtility.AttachmentKey, "89BDF3DB-896C-448D-A84E-872CBA8DBC9F" },
                }
            );
        var attachmentProvider = new Mock<IMessageAttachmentProvider>();
        attachmentProvider
            .Setup(x =>
                x.GetAttachmentAsync(
                    AutoMessageMapper.GetQueueName<AttachmentCommand>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(attachment);
        var middleware = new AttachmentMiddleware(attachmentProvider.Object);
        //act
        await middleware.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        stream.CanRead.Should().BeFalse("It should have been disposed");
        message.Attachment.Filename.Should().Be("test.txt");
        message.Attachment.ContentType.Should().Be("text/plain");
        nextProcessor.Verify(
            x => x.ProcessAsync(stateHandler.Object, CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task Should_delete_attachment_when_finished()
    {
        //arrange
        var message = new AttachmentCommand();
        var nextProcessor = new Mock<IMessageProcessor>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is a stream"));
        var attachment = new MessageAttachment("test.txt", "text/plain", stream);
        var stateHandler = new Mock<IMessageStateHandler<AttachmentCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        stateHandler
            .Setup(x => x.MessageProperties)
            .Returns(
                new Dictionary<string, string>
                {
                    { AttachmentUtility.AttachmentKey, "89BDF3DB-896C-448D-A84E-872CBA8DBC9F" },
                }
            );
        var attachmentProvider = new Mock<IMessageAttachmentProvider>();
        attachmentProvider
            .Setup(x =>
                x.GetAttachmentAsync(
                    AutoMessageMapper.GetQueueName<AttachmentCommand>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(attachment);
        var middleware = new AttachmentMiddleware(attachmentProvider.Object);
        //act
        await middleware.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        attachmentProvider.Verify(
            x =>
                x.DeleteAttachmentAsync(
                    AutoMessageMapper.GetQueueName<AttachmentCommand>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task Should_dispose_the_attachment_stream_exactly_once()
    {
        //arrange
        var message = new AttachmentCommand();
        var nextProcessor = new Mock<IMessageProcessor>();
        var stream = new DisposeCountingStream();
        var attachment = new MessageAttachment("test.txt", "text/plain", stream);
        var stateHandler = new Mock<IMessageStateHandler<AttachmentCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        stateHandler
            .Setup(x => x.MessageProperties)
            .Returns(
                new Dictionary<string, string>
                {
                    { AttachmentUtility.AttachmentKey, "89BDF3DB-896C-448D-A84E-872CBA8DBC9F" },
                }
            );
        var attachmentProvider = new Mock<IMessageAttachmentProvider>();
        attachmentProvider
            .Setup(x =>
                x.GetAttachmentAsync(
                    AutoMessageMapper.GetQueueName<AttachmentCommand>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(attachment);
        var middleware = new AttachmentMiddleware(attachmentProvider.Object);

        //act
        await middleware.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );

        //assert
        stream.DisposeCount.Should().Be(1);
    }

    [Test]
    public async Task Should_not_throw_when_attachment_delete_fails()
    {
        //arrange: by the time the attachment is deleted the message is already completed,
        //so a delete failure must not escape into the error handling and trigger an abandon
        var message = new AttachmentCommand();
        var nextProcessor = new Mock<IMessageProcessor>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is a stream"));
        var attachment = new MessageAttachment("test.txt", "text/plain", stream);
        var stateHandler = new Mock<IMessageStateHandler<AttachmentCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        stateHandler
            .Setup(x => x.MessageProperties)
            .Returns(
                new Dictionary<string, string>
                {
                    { AttachmentUtility.AttachmentKey, "89BDF3DB-896C-448D-A84E-872CBA8DBC9F" },
                }
            );
        var attachmentProvider = new Mock<IMessageAttachmentProvider>();
        attachmentProvider
            .Setup(x =>
                x.GetAttachmentAsync(
                    AutoMessageMapper.GetQueueName<AttachmentCommand>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(attachment);
        attachmentProvider
            .Setup(x =>
                x.DeleteAttachmentAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new TimeoutException("delete failed"));
        var log = new Mock<ILogger>();
        var hostConfiguration = new Mock<IHostConfiguration>();
        hostConfiguration.Setup(x => x.Log).Returns(log.Object);
        var pipelineInformation = new Mock<IPipelineInformation>();
        pipelineInformation.Setup(x => x.HostConfiguration).Returns(hostConfiguration.Object);
        var middleware = new AttachmentMiddleware(attachmentProvider.Object);

        //act & assert
        await middleware
            .Awaiting(x =>
                x.ProcessAsync(
                    stateHandler.Object,
                    pipelineInformation.Object,
                    nextProcessor.Object,
                    CancellationToken.None
                )
            )
            .Should()
            .NotThrowAsync("the message is already completed when the delete runs");
        log.Verify(
            x =>
                x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
            Times.Once,
            "the orphaned attachment must be visible in the log"
        );
    }

    private class DisposeCountingStream : MemoryStream
    {
        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    [Test]
    public async Task Should_not_attach_attachments_for_other_commands()
    {
        //arrange
        var message = new TestCommand();
        var nextProcessor = new Mock<IMessageProcessor>();
        var stateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        stateHandler.Setup(x => x.GetMessage()).Returns(message);
        var attachmentProvider = new Mock<IMessageAttachmentProvider>();
        attachmentProvider
            .Setup(x =>
                x.GetAttachmentAsync(
                    AutoMessageMapper.GetQueueName<TestCommand>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(default(IMessageAttachment));
        var middleware = new AttachmentMiddleware(attachmentProvider.Object);
        //act
        await middleware.ProcessAsync(
            stateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        attachmentProvider.Verify(
            x =>
                x.GetAttachmentAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        nextProcessor.Verify(
            x => x.ProcessAsync(stateHandler.Object, CancellationToken.None),
            Times.Once
        );
    }
}
