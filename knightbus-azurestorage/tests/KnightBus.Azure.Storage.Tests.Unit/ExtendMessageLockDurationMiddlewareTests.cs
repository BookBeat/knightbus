using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit
{
    [TestFixture]
    public class ExtendMessageLockDurationMiddlewareTests
    {
        public async Task Should_renew_lock()
        {
            //arrange
            var middleware = new ExtendMessageLockDurationMiddleware();
            
            
            var pipeline = new Mock<IPipelineInformation>();
            var next = new Mock<IMessageProcessor>();
            
            //act

            await middleware.ProcessAsync(lockHandler.Object, pipeline.Object, next.Object,CancellationToken.None);

            //assert
        }
    }
}