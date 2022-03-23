using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Processors;
using KnightBus.Host.Tests.Unit.ExampleProcessors;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit.MessageProcessing.Processors
{
    public class StreamRequestProcessorTests
    {
        [Test]
        public async Task Should_process_request()
        {
            //arrange
            var request = new TestRequest();
            var response = AsyncEnumerable.Empty<TestResponse>();
            var requestProcessor = new StreamRequestProcessor<TestResponse>(typeof(StreamRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .Returns(response);
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>(typeof(StreamRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            await requestProcessor.ProcessAsync(messageStateHandler.Object, CancellationToken.None);
            //assert
            commandHandler.Verify(x => x.ProcessAsync(request, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task Should_complete_successful_request()
        {
            //arrange
            var request = new TestRequest();
            var response = AsyncEnumerable.Empty<TestResponse>();
            var requestProcessor = new StreamRequestProcessor<TestResponse>(typeof(StreamRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .Returns(response);
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>(typeof(StreamRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            await requestProcessor.ProcessAsync(messageStateHandler.Object, CancellationToken.None);
            //assert
            messageStateHandler.Verify(x => x.CompleteAsync(), Times.Once);
        }

        [Test]
        public async Task Should_reply_successful_request()
        {
            //arrange
            var request = new TestRequest();
            var response = AsyncEnumerable.Empty<TestResponse>()
                .Append(new TestResponse())
                .Append(new TestResponse());
            var requestProcessor = new StreamRequestProcessor<TestResponse>(typeof(StreamRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .Returns(response);
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>(typeof(StreamRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            await requestProcessor.ProcessAsync(messageStateHandler.Object, CancellationToken.None);
            //assert
            messageStateHandler.Verify(x => x.ReplyAsync(It.IsAny<TestResponse>()), Times.Exactly(2));
        }

        [Test]
        public void Should_not_complete_failed_request()
        {
            //arrange
            var request = new TestRequest();
            var requestProcessor = new StreamRequestProcessor<TestResponse>(typeof(StreamRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .Throws(new Exception());
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>(typeof(StreamRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            requestProcessor.Awaiting(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Should().ThrowAsync<Exception>();
            //assert
            messageStateHandler.Verify(x => x.CompleteAsync(), Times.Never);
        }

        [Test]
        public void Should_not_reply_failed_request()
        {
            //arrange
            var request = new TestRequest();
            var response = new TestResponse();
            var requestProcessor = new StreamRequestProcessor<TestResponse>(typeof(StreamRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .Throws(new Exception());
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, IAsyncEnumerable<TestResponse>>>(typeof(StreamRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            requestProcessor.Awaiting(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Should().ThrowAsync<Exception>();
            //assert
            messageStateHandler.Verify(x => x.ReplyAsync(response), Times.Never);
        }
    }
}