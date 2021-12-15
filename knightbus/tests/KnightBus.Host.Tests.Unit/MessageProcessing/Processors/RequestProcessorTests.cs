using System;
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
    [TestFixture]
    public class RequestProcessorTests
    {
        [Test]
        public async Task Should_process_request()
        {
            //arrange
            var request = new TestRequest();
            var requestProcessor = new RequestProcessor<TestResponse>(typeof(SingleRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, Task<TestResponse>>>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, Task<TestResponse>>>(typeof(SingleRequestProcessor)))
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
            var requestProcessor = new RequestProcessor<TestResponse>(typeof(SingleRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, Task<TestResponse>>>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, Task<TestResponse>>>(typeof(SingleRequestProcessor)))
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
            var response = new TestResponse();
            var requestProcessor = new RequestProcessor<TestResponse>(typeof(SingleRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, Task<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .ReturnsAsync(response);
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, Task<TestResponse>>>(typeof(SingleRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            await requestProcessor.ProcessAsync(messageStateHandler.Object, CancellationToken.None);
            //assert
            messageStateHandler.Verify(x => x.ReplyAsync(response), Times.Once);
        }

        [Test]
        public void Should_not_complete_failed_request()
        {
            //arrange
            var request = new TestRequest();
            var requestProcessor = new RequestProcessor<TestResponse>(typeof(SingleRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, Task<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .ThrowsAsync(new Exception());
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, Task<TestResponse>>>(typeof(SingleRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            requestProcessor.Awaiting(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Should().Throw<Exception>();
            //assert
            messageStateHandler.Verify(x => x.CompleteAsync(), Times.Never);
        }

        [Test]
        public void Should_not_reply_failed_request()
        {
            //arrange
            var request = new TestRequest();
            var response = new TestResponse();
            var requestProcessor = new RequestProcessor<TestResponse>(typeof(SingleRequestProcessor));
            var commandHandler = new Mock<IProcessMessage<TestRequest, Task<TestResponse>>>();
            commandHandler.Setup(x => x.ProcessAsync(request, CancellationToken.None))
                .ThrowsAsync(new Exception());
            var messageStateHandler = new Mock<IMessageStateHandler<TestRequest>>();
            messageStateHandler
                .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestRequest, Task<TestResponse>>>(typeof(SingleRequestProcessor)))
                .Returns(commandHandler.Object);
            messageStateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(request);
            //act
            requestProcessor.Awaiting(x=> x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Should().Throw<Exception>();
            //assert
            messageStateHandler.Verify(x => x.ReplyAsync(response), Times.Never);
        }
    }
}